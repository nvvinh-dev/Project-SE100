import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type { CreatePlotRequest, PlotResponse, UpdatePlotRequest } from "@/features/plots/types";
import { ApiError } from "@/lib/api-error";
import { recordStatuses, soilTypes, waterSources } from "@/lib/enums";
import { setFieldErrors } from "@/lib/form-errors";
import { parseDecimalInput } from "@/lib/format";

// Kiểm tra sớm trên form để báo lỗi ngay dưới ô; backend vẫn kiểm tra lại. Giới hạn theo cột trong database.

const CODE_MAX = 20;
const OTHER_MAX = 200;
// Cột numeric(18,2): tối đa 16 chữ số phần nguyên, 2 chữ số sau dấu phẩy.
const AREA_PATTERN = /^\d{1,16}(?:[.,]\d{1,2})?$/;

// Ô chọn giữ chuỗi ("" khi chưa chọn); kiểm tra thuộc enum rồi thu hẹp kiểu ở đầu ra.
function oneOf<T extends string>(values: readonly T[], error: string) {
  return z.string().refine((value): value is T => (values as readonly string[]).includes(value), { error });
}

/** Form tạo và sửa thửa dùng chung: khi tạo mã để trống được (tự sinh, BR-CAT-01), khi sửa phải có mã. */
export function plotFormSchema(mode: "create" | "edit") {
  return z.object({
    code:
      mode === "create"
        ? z.string().trim().max(CODE_MAX, { error: "Mã thửa tối đa 20 ký tự." })
        : z
            .string()
            .trim()
            .min(1, { error: "Vui lòng nhập mã thửa.", abort: true })
            .max(CODE_MAX, { error: "Mã thửa tối đa 20 ký tự." }),
    areaM2: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập diện tích.", abort: true })
      .refine((text) => parseDecimalInput(text) !== null, {
        error: "Diện tích phải là số, ví dụ 1500 hoặc 1500,5.",
        abort: true,
      })
      .refine((text) => (parseDecimalInput(text) ?? 0) > 0, { error: "Diện tích phải lớn hơn 0.", abort: true })
      .refine((text) => AREA_PATTERN.test(text), { error: "Diện tích tối đa 2 chữ số sau dấu phẩy." }),
    locationDescription: z.string().trim(),
    soilType: oneOf(soilTypes, "Vui lòng chọn loại đất."),
    soilTypeOther: z.string().trim().max(OTHER_MAX, { error: "Ghi chú loại đất tối đa 200 ký tự." }),
    waterSource: oneOf(waterSources, "Vui lòng chọn nguồn nước."),
    waterSourceOther: z.string().trim().max(OTHER_MAX, { error: "Ghi chú nguồn nước tối đa 200 ký tự." }),
    // Form tạo không hiện ô này: thửa mới luôn đang sử dụng.
    status: oneOf(recordStatuses, "Vui lòng chọn tình trạng sử dụng."),
    note: z.string().trim(),
  });
}

/** Giá trị trong ô nhập (chuỗi). */
export type PlotFormValues = z.input<ReturnType<typeof plotFormSchema>>;
/** Giá trị sau khi kiểm tra, enum đã đúng kiểu. */
export type PlotFormOutput = z.output<ReturnType<typeof plotFormSchema>>;

export const emptyPlotFormValues: PlotFormValues = {
  code: "",
  areaM2: "",
  locationDescription: "",
  soilType: "",
  soilTypeOther: "",
  waterSource: "",
  waterSourceOther: "",
  status: "Active",
  note: "",
};

/** Điền form sửa từ thửa đang có. Số dùng dấu phẩy thập phân như người dùng quen gõ. */
export function plotToFormValues(plot: PlotResponse): PlotFormValues {
  return {
    code: plot.code,
    areaM2: plot.areaM2 === null ? "" : String(plot.areaM2).replace(".", ","),
    locationDescription: plot.locationDescription ?? "",
    soilType: plot.soilType ?? "",
    soilTypeOther: plot.soilTypeOther ?? "",
    waterSource: plot.waterSource ?? "",
    waterSourceOther: plot.waterSourceOther ?? "",
    status: plot.status ?? "Active",
    note: plot.note ?? "",
  };
}

// Ô trống gửi null; ghi chú "Khác" chỉ gửi khi chọn "Khác".
function emptyToNull(text: string): string | null {
  return text === "" ? null : text;
}

export function toCreatePlotRequest(values: PlotFormOutput): CreatePlotRequest {
  return {
    code: emptyToNull(values.code),
    // Schema đã bảo đảm diện tích là số hợp lệ.
    areaM2: parseDecimalInput(values.areaM2) ?? 0,
    locationDescription: emptyToNull(values.locationDescription),
    soilType: values.soilType,
    soilTypeOther: values.soilType === "Other" ? emptyToNull(values.soilTypeOther) : null,
    waterSource: values.waterSource,
    waterSourceOther: values.waterSource === "Other" ? emptyToNull(values.waterSourceOther) : null,
    note: emptyToNull(values.note),
  };
}

export function toUpdatePlotRequest(values: PlotFormOutput, version: number): UpdatePlotRequest {
  return { ...toCreatePlotRequest(values), code: values.code, status: values.status, version };
}

const formFields = [
  "code",
  "areaM2",
  "locationDescription",
  "soilType",
  "soilTypeOther",
  "waterSource",
  "waterSourceOther",
  "status",
  "note",
] as const satisfies readonly (keyof PlotFormValues)[];

/**
 * Gắn lỗi backend vào đúng ô: `VALIDATION_ERROR` theo từng trường, `CODE_ALREADY_USED` vào ô mã thửa.
 * Trả `false` khi không gắn được, trang hiện lỗi chung trên form.
 */
export function setPlotFieldErrors(error: unknown, setError: UseFormSetError<PlotFormValues>): boolean {
  if (error instanceof ApiError && error.code === "CODE_ALREADY_USED") {
    setError("code", { type: "server", message: error.message });
    return true;
  }
  return setFieldErrors(error, setError, formFields);
}
