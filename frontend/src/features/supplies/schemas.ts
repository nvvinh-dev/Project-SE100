import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type {
  CreateStockTransactionRequest,
  CreateSupplyRequest,
  SupplyResponse,
  UpdateSupplyRequest,
} from "@/features/supplies/types";
import { ApiError } from "@/lib/api-error";
import { recordStatuses, supplyGroups, units } from "@/lib/enums";
import { setFieldErrors } from "@/lib/form-errors";
import { parseDecimalInput } from "@/lib/format";

// Kiểm tra sớm trên form để báo lỗi ngay dưới ô; backend vẫn kiểm tra lại. Giới hạn theo cột trong database.

const CODE_MAX = 20;
const NAME_MAX = 200;
// Cột numeric(18,3), numeric(18,4).
const QUANTITY_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;
const PRICE_PATTERN = /^\d{1,14}(?:[.,]\d{1,4})?$/;

// Ô chọn giữ chuỗi ("" khi chưa chọn); kiểm tra thuộc enum rồi thu hẹp kiểu ở đầu ra.
function oneOf<T extends string>(values: readonly T[], error: string) {
  return z.string().refine((value): value is T => (values as readonly string[]).includes(value), { error });
}

// Ô số không bắt buộc: để trống được; gõ gì thì phải là số không âm, đúng số chữ số của cột.
function optionalNonNegative(pattern: RegExp, label: string, precision: string) {
  return z
    .string()
    .trim()
    .refine((text) => text === "" || parseDecimalInput(text) !== null, {
      error: `${label} phải là số, ví dụ 1500 hoặc 1500,5.`,
      abort: true,
    })
    .refine((text) => text === "" || (parseDecimalInput(text) ?? 0) >= 0, { error: `${label} không được âm.`, abort: true })
    .refine((text) => text === "" || pattern.test(text), { error: `${label} tối đa ${precision} chữ số sau dấu phẩy.` });
}

/** Form tạo và sửa vật tư dùng chung: khi tạo mã để trống được (tự sinh, BR-CAT-01), khi sửa phải có mã. */
export function supplyFormSchema(mode: "create" | "edit") {
  return z.object({
    code:
      mode === "create"
        ? z.string().trim().max(CODE_MAX, { error: "Mã vật tư tối đa 20 ký tự." })
        : z
            .string()
            .trim()
            .min(1, { error: "Vui lòng nhập mã vật tư.", abort: true })
            .max(CODE_MAX, { error: "Mã vật tư tối đa 20 ký tự." }),
    name: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập tên vật tư.", abort: true })
      .max(NAME_MAX, { error: "Tên vật tư tối đa 200 ký tự." }),
    group: oneOf(supplyGroups, "Vui lòng chọn nhóm vật tư."),
    unit: oneOf(units, "Vui lòng chọn đơn vị tính."),
    minStockLevel: optionalNonNegative(QUANTITY_PATTERN, "Ngưỡng tồn tối thiểu", "3"),
    referencePrice: optionalNonNegative(PRICE_PATTERN, "Giá nhập tham khảo", "4"),
    // Form tạo không hiện ô này: vật tư mới luôn đang sử dụng.
    status: oneOf(recordStatuses, "Vui lòng chọn trạng thái."),
    note: z.string().trim(),
  });
}

/** Giá trị trong ô nhập (chuỗi). */
export type SupplyFormValues = z.input<ReturnType<typeof supplyFormSchema>>;
/** Giá trị sau khi kiểm tra, enum đã đúng kiểu. */
export type SupplyFormOutput = z.output<ReturnType<typeof supplyFormSchema>>;

export const emptySupplyFormValues: SupplyFormValues = {
  code: "",
  name: "",
  group: "",
  unit: "",
  minStockLevel: "",
  referencePrice: "",
  status: "Active",
  note: "",
};

// Số dùng dấu phẩy thập phân như người dùng quen gõ.
function numberToInput(value: number | undefined): string {
  return value === undefined ? "" : String(value).replace(".", ",");
}

/** Điền form sửa từ vật tư đang có. */
export function supplyToFormValues(supply: SupplyResponse): SupplyFormValues {
  return {
    code: supply.code,
    name: supply.name,
    group: supply.group,
    unit: supply.unit,
    minStockLevel: numberToInput(supply.minStockLevel),
    referencePrice: numberToInput(supply.referencePrice),
    status: supply.status,
    note: supply.note ?? "",
  };
}

// Ô trống gửi null.
function emptyToNull(text: string): string | null {
  return text === "" ? null : text;
}

export function toCreateSupplyRequest(values: SupplyFormOutput): CreateSupplyRequest {
  return {
    code: emptyToNull(values.code),
    name: values.name,
    group: values.group,
    unit: values.unit,
    // Để trống ngưỡng nghĩa là 0 (mặc định của cột).
    minStockLevel: parseDecimalInput(values.minStockLevel) ?? 0,
    referencePrice: parseDecimalInput(values.referencePrice),
    note: emptyToNull(values.note),
  };
}

export function toUpdateSupplyRequest(values: SupplyFormOutput, version: number): UpdateSupplyRequest {
  return { ...toCreateSupplyRequest(values), code: values.code, status: values.status, version };
}

const formFields = [
  "code",
  "name",
  "group",
  "unit",
  "minStockLevel",
  "referencePrice",
  "status",
  "note",
] as const satisfies readonly (keyof SupplyFormValues)[];

/**
 * Gắn lỗi backend vào đúng ô: `VALIDATION_ERROR` theo từng trường, `CODE_ALREADY_USED` vào ô mã, `UNIT_LOCKED` vào ô đơn vị.
 * Trả `false` khi không gắn được, trang hiện lỗi chung trên form.
 */
export function setSupplyFieldErrors(error: unknown, setError: UseFormSetError<SupplyFormValues>): boolean {
  if (error instanceof ApiError && error.code === "CODE_ALREADY_USED") {
    setError("code", { type: "server", message: error.message });
    return true;
  }
  if (error instanceof ApiError && error.code === "UNIT_LOCKED") {
    setError("unit", { type: "server", message: error.message });
    return true;
  }
  return setFieldErrors(error, setError, formFields);
}

/** Ngày hôm nay theo giờ Việt Nam dạng `yyyy-MM-dd`, không theo múi giờ của máy (BR-GEN-05). */
export function todayInVietnam(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(new Date());
}

// Ô số lượng bắt buộc của form kho: số dương, đúng số chữ số của cột quantity.
const positiveQuantity = z
  .string()
  .trim()
  .min(1, { error: "Vui lòng nhập số lượng.", abort: true })
  .refine((text) => parseDecimalInput(text) !== null, { error: "Số lượng phải là số, ví dụ 10 hoặc 10,5.", abort: true })
  .refine((text) => (parseDecimalInput(text) ?? 0) > 0, { error: "Số lượng phải lớn hơn 0.", abort: true })
  .refine((text) => QUANTITY_PATTERN.test(text), { error: "Số lượng tối đa 3 chữ số sau dấu phẩy." });

const transactionDate = z
  .string()
  .min(1, { error: "Vui lòng chọn ngày.", abort: true })
  .refine((date) => date <= todayInVietnam(), { error: "Ngày không được sau hôm nay." });

export const stockInFormSchema = z.object({
  supplyId: z.string().min(1, { error: "Vui lòng chọn vật tư." }),
  quantity: positiveQuantity,
  unitCost: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập đơn giá nhập.", abort: true })
    .refine((text) => parseDecimalInput(text) !== null, { error: "Đơn giá phải là số, ví dụ 20000.", abort: true })
    .refine((text) => (parseDecimalInput(text) ?? 0) >= 0, { error: "Đơn giá nhập không được âm.", abort: true })
    .refine((text) => PRICE_PATTERN.test(text), { error: "Đơn giá tối đa 4 chữ số sau dấu phẩy." }),
  transactionDate,
  note: z.string().trim(),
});

export type StockInFormValues = z.input<typeof stockInFormSchema>;
export type StockInFormOutput = z.output<typeof stockInFormSchema>;

export function toStockInRequest(values: StockInFormOutput, requestId: string): CreateStockTransactionRequest {
  return {
    type: "In",
    supplyId: values.supplyId,
    // Schema đã bảo đảm hai ô số hợp lệ.
    quantity: parseDecimalInput(values.quantity) ?? 0,
    unitCost: parseDecimalInput(values.unitCost) ?? 0,
    transactionDate: values.transactionDate,
    seasonId: null,
    reason: null,
    note: emptyToNull(values.note),
    requestId,
  };
}

export const stockAdjustFormSchema = z.object({
  supplyId: z.string().min(1, { error: "Vui lòng chọn vật tư." }),
  direction: z.enum(["increase", "decrease"]),
  quantity: positiveQuantity,
  // BR-GEN-06: lý do không rỗng sau khi bỏ khoảng trắng.
  reason: z.string().trim().min(1, { error: "Vui lòng nhập lý do điều chỉnh." }),
  transactionDate,
  note: z.string().trim(),
});

export type StockAdjustFormValues = z.input<typeof stockAdjustFormSchema>;
export type StockAdjustFormOutput = z.output<typeof stockAdjustFormSchema>;

export function toStockAdjustRequest(values: StockAdjustFormOutput, requestId: string): CreateStockTransactionRequest {
  const quantity = parseDecimalInput(values.quantity) ?? 0;
  return {
    type: "Adjustment",
    supplyId: values.supplyId,
    // BR-SUP-09: có dấu, tăng dương, giảm âm.
    quantity: values.direction === "increase" ? quantity : -quantity,
    unitCost: null,
    transactionDate: values.transactionDate,
    seasonId: null,
    reason: values.reason,
    note: emptyToNull(values.note),
    requestId,
  };
}

export const stockOutFormSchema = z.object({
  supplyId: z.string().min(1, { error: "Vui lòng chọn vật tư." }),
  // BR-SUP-06: xuất kho luôn gắn một mùa vụ.
  seasonId: z.string().min(1, { error: "Vui lòng chọn mùa vụ." }),
  quantity: positiveQuantity,
  // Được trước ngày bắt đầu mùa vụ (bón lót, chuẩn bị đất), chỉ không được sau hôm nay.
  transactionDate,
  note: z.string().trim(),
});

export type StockOutFormValues = z.input<typeof stockOutFormSchema>;
export type StockOutFormOutput = z.output<typeof stockOutFormSchema>;

export function toStockOutRequest(values: StockOutFormOutput, requestId: string): CreateStockTransactionRequest {
  return {
    type: "Out",
    supplyId: values.supplyId,
    // Gửi lượng dương; backend lưu thành số âm.
    quantity: parseDecimalInput(values.quantity) ?? 0,
    // BR-SUP-06: đơn giá là giá vốn bình quân, backend tự lấy.
    unitCost: null,
    transactionDate: values.transactionDate,
    seasonId: values.seasonId,
    reason: null,
    note: emptyToNull(values.note),
    requestId,
  };
}
