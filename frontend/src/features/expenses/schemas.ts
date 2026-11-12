import { z } from "zod";
import type { CorrectExpenseRequest, CreateExpenseRequest, ExpenseResponse } from "@/features/expenses/types";
import { todayInVietnam } from "@/features/supplies/schemas";
import { expenseCategories, type ExpenseCategory } from "@/lib/enums";
import { parseDecimalInput } from "@/lib/format";

// Kiểm tra sớm trên form để báo lỗi ngay dưới ô; backend vẫn kiểm tra lại. Giới hạn theo cột trong database.
// Không dùng `abort`: một ô bị dừng kiểm tra thì Zod bỏ luôn kiểm tra cấp form (ô mùa vụ), lỗi chọn mùa vụ sẽ không hiện
// cùng lúc với lỗi các ô khác. Mỗi ô chỉ hiện lỗi đầu tiên nên các kiểm tra sau không làm thêm thông báo.

const CATEGORY_OTHER_MAX = 200;
// Cột amount numeric(18,2).
const AMOUNT_PATTERN = /^\d{1,16}(?:[.,]\d{1,2})?$/;

/** Chi phí gắn một mùa vụ, hoặc chi phí chung của hộ (không gắn mùa vụ). */
export const expenseScopes = ["season", "general"] as const;

export const expenseFormSchema = z
  .object({
    scope: z.enum(expenseScopes),
    seasonId: z.string(),
    // BR-EXP-01: chỉ bốn nhóm của ExpenseCategory, không có nhóm vật tư.
    category: z.string().refine((value): value is ExpenseCategory => (expenseCategories as readonly string[]).includes(value), {
      error: "Vui lòng chọn nhóm chi phí.",
    }),
    categoryOther: z.string().trim().max(CATEGORY_OTHER_MAX, { error: "Ghi chú nhóm chi phí tối đa 200 ký tự." }),
    amount: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập số tiền." })
      .refine((text) => parseDecimalInput(text) !== null, { error: "Số tiền phải là số, ví dụ 150000." })
      .refine((text) => (parseDecimalInput(text) ?? 0) > 0, { error: "Số tiền phải lớn hơn 0." })
      .refine((text) => AMOUNT_PATTERN.test(text), { error: "Số tiền tối đa 2 chữ số sau dấu phẩy." }),
    // BR-GEN-05: không sau hôm nay; BR-EXP-02: được trước ngày bắt đầu mùa vụ.
    expenseDate: z
      .string()
      .min(1, { error: "Vui lòng chọn ngày phát sinh." })
      .refine((date) => date <= todayInVietnam(), { error: "Ngày phát sinh không được sau hôm nay." }),
    note: z.string().trim(),
  })
  .refine((values) => values.scope === "general" || values.seasonId !== "", {
    path: ["seasonId"],
    error: "Vui lòng chọn mùa vụ, hoặc chọn chi phí chung của hộ.",
  });

/** Giá trị trong ô nhập (chuỗi). */
export type ExpenseFormValues = z.input<typeof expenseFormSchema>;
/** Giá trị sau khi kiểm tra. */
export type ExpenseFormOutput = z.output<typeof expenseFormSchema>;

/** Ô của form, theo thứ tự, để gắn lỗi `VALIDATION_ERROR` của backend. */
export const expenseFormFields = [
  "seasonId",
  "category",
  "categoryOther",
  "amount",
  "expenseDate",
  "note",
] as const satisfies readonly (keyof ExpenseFormValues)[];

export function emptyExpenseFormValues(): ExpenseFormValues {
  return {
    scope: "season",
    seasonId: "",
    category: "",
    categoryOther: "",
    amount: "",
    expenseDate: todayInVietnam(),
    note: "",
  };
}

/** Điền form điều chỉnh từ khoản chi đang có; số tiền dùng dấu phẩy thập phân như người dùng quen gõ. */
export function expenseToFormValues(expense: ExpenseResponse): ExpenseFormValues {
  return {
    scope: expense.seasonId ? "season" : "general",
    seasonId: expense.seasonId ?? "",
    category: expense.category,
    categoryOther: expense.categoryOther ?? "",
    amount: String(expense.amount).replace(".", ","),
    expenseDate: expense.expenseDate,
    note: expense.note ?? "",
  };
}

// Phần dữ liệu chung của yêu cầu ghi và điều chỉnh.
function toExpenseData(values: ExpenseFormOutput) {
  // Schema đã bảo đảm số tiền hợp lệ.
  const category = values.category;
  return {
    seasonId: values.scope === "season" ? values.seasonId : null,
    category,
    // Ghi chú nhóm chỉ có nghĩa với nhóm "Khác".
    categoryOther: category === "Other" && values.categoryOther !== "" ? values.categoryOther : null,
    amount: parseDecimalInput(values.amount) ?? 0,
    expenseDate: values.expenseDate,
    note: values.note === "" ? null : values.note,
  };
}

export function toCreateExpenseRequest(values: ExpenseFormOutput, requestId: string): CreateExpenseRequest {
  return { ...toExpenseData(values), requestId };
}

export function toCorrectExpenseRequest(values: ExpenseFormOutput, reason: string, version: number): CorrectExpenseRequest {
  return { ...toExpenseData(values), reason, version };
}
