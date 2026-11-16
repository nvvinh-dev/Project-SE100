import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type { CreateCustomerRequest, CreateSaleRequest, CustomerResponse, UpdateCustomerRequest } from "@/features/sales/types";
import { todayInVietnam } from "@/features/supplies/schemas";
import { qualityGrades, recordStatuses, salesChannels } from "@/lib/enums";
import { setFieldErrors } from "@/lib/form-errors";
import { parseDecimalInput } from "@/lib/format";
import { normalizePhone, PHONE_INVALID_MESSAGE } from "@/lib/phone";

// Kiểm tra sớm trên form để báo lỗi ngay dưới ô; backend vẫn kiểm tra lại. Giới hạn theo cột trong database.

// Ô chọn giữ chuỗi ("" khi chưa chọn); kiểm tra thuộc enum rồi thu hẹp kiểu ở đầu ra.
export function oneOf<T extends string>(values: readonly T[], error: string) {
  return z.string().refine((value): value is T => (values as readonly string[]).includes(value), { error });
}

export function emptyToNull(text: string): string | null {
  return text === "" ? null : text;
}

export const customerFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập tên khách hàng.", abort: true })
    .max(200, { error: "Tên khách hàng tối đa 200 ký tự." }),
  phone: z
    .string()
    .trim()
    .refine((text) => text === "" || normalizePhone(text) !== null, { error: PHONE_INVALID_MESSAGE }),
  note: z.string().trim(),
  status: oneOf(recordStatuses, "Vui lòng chọn tình trạng sử dụng."),
});

export type CustomerFormValues = z.input<typeof customerFormSchema>;
export type CustomerFormOutput = z.output<typeof customerFormSchema>;

export const emptyCustomerFormValues: CustomerFormValues = { name: "", phone: "", note: "", status: "Active" };

export function customerToFormValues(customer: CustomerResponse): CustomerFormValues {
  return {
    name: customer.name,
    phone: customer.phone ?? "",
    note: customer.note ?? "",
    status: customer.status,
  };
}

export function toCreateCustomerRequest(values: CustomerFormOutput): CreateCustomerRequest {
  return {
    name: values.name,
    phone: values.phone === "" ? null : normalizePhone(values.phone),
    note: emptyToNull(values.note),
    status: values.status,
  };
}

export function toUpdateCustomerRequest(values: CustomerFormOutput, version: number): UpdateCustomerRequest {
  return { ...toCreateCustomerRequest(values), version };
}

const customerFormFields = ["name", "phone", "note", "status"] as const satisfies readonly (keyof CustomerFormValues)[];

/** Gắn lỗi `VALIDATION_ERROR` vào từng ô; trả `false` khi không gắn được, trang hiện lỗi chung trên form. */
export function setCustomerFieldErrors(error: unknown, setError: UseFormSetError<CustomerFormValues>): boolean {
  return setFieldErrors(error, setError, customerFormFields);
}

// ---- Giao dịch bán ----

// Cột quantity numeric(18,3), unit_price numeric(18,4).
const SALE_QUANTITY_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;
const UNIT_PRICE_PATTERN = /^\d{1,14}(?:[.,]\d{1,4})?$/;

// Không dùng `abort`: một ô bị dừng kiểm tra thì Zod bỏ luôn kiểm tra cấp form (khách hàng hoặc kênh bán).
export const saleFormSchema = z
  .object({
    seasonId: z.string().min(1, { error: "Vui lòng chọn mùa vụ." }),
    qualityGrade: oneOf(qualityGrades, "Vui lòng chọn loại chất lượng."),
    customerId: z.string(),
    channel: z.string().refine((value) => value === "" || (salesChannels as readonly string[]).includes(value), {
      error: "Kênh bán không hợp lệ.",
    }),
    channelOther: z.string().trim().max(200, { error: "Ghi chú kênh bán tối đa 200 ký tự." }),
    quantity: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập số lượng." })
      .refine((text) => parseDecimalInput(text) !== null, { error: "Số lượng phải là số, ví dụ 200 hoặc 200,5." })
      .refine((text) => (parseDecimalInput(text) ?? 0) > 0, { error: "Số lượng phải lớn hơn 0." })
      .refine((text) => SALE_QUANTITY_PATTERN.test(text), { error: "Số lượng tối đa 3 chữ số sau dấu phẩy." }),
    unitPrice: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập đơn giá." })
      .refine((text) => parseDecimalInput(text) !== null, { error: "Đơn giá phải là số, ví dụ 8500." })
      .refine((text) => (parseDecimalInput(text) ?? -1) >= 0, { error: "Đơn giá không được âm." })
      .refine((text) => UNIT_PRICE_PATTERN.test(text), { error: "Đơn giá tối đa 4 chữ số sau dấu phẩy." }),
    // BR-GEN-05: không sau hôm nay; không trước ngày bắt đầu mùa vụ do backend kiểm tra (BR-SAL-01).
    saleDate: z
      .string()
      .min(1, { error: "Vui lòng chọn ngày bán." })
      .refine((date) => date <= todayInVietnam(), { error: "Ngày bán không được sau hôm nay." }),
    note: z.string().trim(),
    harvestRecordIds: z.array(z.string()),
  })
  // BR-SAL-01: có ít nhất khách hàng hoặc kênh bán.
  .refine((values) => values.customerId !== "" || values.channel !== "", {
    path: ["channel"],
    error: "Vui lòng chọn khách hàng hoặc kênh bán.",
  });

export type SaleFormValues = z.input<typeof saleFormSchema>;
export type SaleFormOutput = z.output<typeof saleFormSchema>;

/** Ô của form, theo thứ tự, để gắn lỗi `VALIDATION_ERROR` của backend. */
export const saleFormFields = [
  "seasonId",
  "qualityGrade",
  "customerId",
  "channel",
  "channelOther",
  "quantity",
  "unitPrice",
  "saleDate",
  "note",
  "harvestRecordIds",
] as const satisfies readonly (keyof SaleFormValues)[];

/** Form trống; `grade` lấy từ `?grade=` khi đi từ trang thu hoạch ("Bán", "Bán ngay"). */
export function emptySaleFormValues(grade: string | null): SaleFormValues {
  return {
    seasonId: "",
    qualityGrade: grade && (qualityGrades as readonly string[]).includes(grade) ? grade : "",
    customerId: "",
    channel: "",
    channelOther: "",
    quantity: "",
    unitPrice: "",
    saleDate: todayInVietnam(),
    note: "",
    harvestRecordIds: [],
  };
}

export function toCreateSaleRequest(values: SaleFormOutput, requestId: string): CreateSaleRequest {
  const channel = salesChannels.find((value) => value === values.channel) ?? null;
  return {
    requestId,
    seasonId: values.seasonId,
    customerId: emptyToNull(values.customerId),
    channel,
    // Ghi chú kênh chỉ có nghĩa với kênh "Khác".
    channelOther: channel === "Other" ? emptyToNull(values.channelOther) : null,
    qualityGrade: values.qualityGrade,
    // Schema đã bảo đảm số hợp lệ.
    quantity: parseDecimalInput(values.quantity) ?? 0,
    unitPrice: parseDecimalInput(values.unitPrice) ?? 0,
    saleDate: values.saleDate,
    note: emptyToNull(values.note),
    harvestRecordIds: values.harvestRecordIds,
  };
}

/** Thành tiền tính sẵn trên form: số lượng × đơn giá, làm tròn 2 chữ số như backend; `null` khi chưa nhập đủ. */
export function previewRevenue(quantityText: string, unitPriceText: string): number | null {
  const quantity = parseDecimalInput(quantityText.trim());
  const unitPrice = parseDecimalInput(unitPriceText.trim());
  if (quantity === null || unitPrice === null || quantity <= 0 || unitPrice < 0) {
    return null;
  }
  return Math.round(quantity * unitPrice * 100) / 100;
}
