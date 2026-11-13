import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type { CreateCropRequest, CropResponse, UpdateCropRequest } from "@/features/crops/types";
import { ApiError } from "@/lib/api-error";
import { cropGroups, recordStatuses, units } from "@/lib/enums";
import { setFieldErrors } from "@/lib/form-errors";

// Kiểm tra sớm trên form để báo lỗi ngay dưới ô; backend vẫn kiểm tra lại. Giới hạn theo cột trong database.

const NAME_MAX = 200;
const OTHER_MAX = 200;

// Ô chọn giữ chuỗi ("" khi chưa chọn); kiểm tra thuộc enum rồi thu hẹp kiểu ở đầu ra.
function oneOf<T extends string>(values: readonly T[], error: string) {
  return z.string().refine((value): value is T => (values as readonly string[]).includes(value), { error });
}

/** Form tạo và sửa cây trồng dùng chung; form tạo không hiện ô trạng thái (cây mới luôn đang sử dụng). */
export const cropFormSchema = z.object({
  name: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập tên cây trồng.", abort: true })
    .max(NAME_MAX, { error: "Tên cây trồng tối đa 200 ký tự." }),
  group: oneOf(cropGroups, "Vui lòng chọn nhóm cây."),
  groupOther: z.string().trim().max(OTHER_MAX, { error: "Ghi chú nhóm cây tối đa 200 ký tự." }),
  yieldUnit: oneOf(units, "Vui lòng chọn đơn vị sản lượng."),
  referenceInfo: z.string().trim(),
  status: oneOf(recordStatuses, "Vui lòng chọn trạng thái."),
});

/** Giá trị trong ô nhập (chuỗi). */
export type CropFormValues = z.input<typeof cropFormSchema>;
/** Giá trị sau khi kiểm tra, enum đã đúng kiểu. */
export type CropFormOutput = z.output<typeof cropFormSchema>;

export const emptyCropFormValues: CropFormValues = {
  name: "",
  group: "",
  groupOther: "",
  yieldUnit: "",
  referenceInfo: "",
  status: "Active",
};

/** Điền form sửa từ cây đang có. */
export function cropToFormValues(crop: CropResponse): CropFormValues {
  return {
    name: crop.name,
    group: crop.group ?? "",
    groupOther: crop.groupOther ?? "",
    yieldUnit: crop.yieldUnit ?? "",
    referenceInfo: crop.referenceInfo ?? "",
    status: crop.status ?? "Active",
  };
}

// Ô trống gửi null; ghi chú "Khác" chỉ gửi khi chọn "Khác".
function emptyToNull(text: string): string | null {
  return text === "" ? null : text;
}

export function toCreateCropRequest(values: CropFormOutput): CreateCropRequest {
  return {
    name: values.name,
    group: values.group,
    groupOther: values.group === "Other" ? emptyToNull(values.groupOther) : null,
    yieldUnit: values.yieldUnit,
    referenceInfo: emptyToNull(values.referenceInfo),
  };
}

export function toUpdateCropRequest(values: CropFormOutput, version: number): UpdateCropRequest {
  return { ...toCreateCropRequest(values), status: values.status, version };
}

const formFields = [
  "name",
  "group",
  "groupOther",
  "yieldUnit",
  "referenceInfo",
  "status",
] as const satisfies readonly (keyof CropFormValues)[];

/**
 * Gắn lỗi backend vào đúng ô: `VALIDATION_ERROR` theo từng trường, `NAME_ALREADY_USED` vào ô tên.
 * Trả `false` khi không gắn được, trang hiện lỗi chung trên form (gồm `UNIT_LOCKED`, `CATALOG_IN_USE`).
 */
export function setCropFieldErrors(error: unknown, setError: UseFormSetError<CropFormValues>): boolean {
  if (error instanceof ApiError && error.code === "NAME_ALREADY_USED") {
    setError("name", { type: "server", message: error.message });
    return true;
  }
  return setFieldErrors(error, setError, formFields);
}
