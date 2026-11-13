import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type { CreateCustomerRequest, CustomerResponse, UpdateCustomerRequest } from "@/features/sales/types";
import { recordStatuses } from "@/lib/enums";
import { setFieldErrors } from "@/lib/form-errors";
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
