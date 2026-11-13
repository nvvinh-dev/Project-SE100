import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type { CreateSeasonRequest, SeasonResponse, UpdateSeasonRequest } from "@/features/seasons/types";
import { setFieldErrors } from "@/lib/form-errors";
import { parseDecimalInput } from "@/lib/format";

// Định dạng ngày hiện tại theo múi giờ Việt Nam (Asia/Ho_Chi_Minh).
export function todayInVietnam(): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Asia/Ho_Chi_Minh" }).format(new Date());
}

// Cột numeric(18,3): tối đa 15 chữ số phần nguyên, 3 chữ số sau dấu phẩy (BR-SEA-01).
const YIELD_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;

/** Form tạo và sửa mùa vụ (FR-SEA-01, FR-SEA-07). */
export function seasonFormSchema() {
  return z
    .object({
      plotId: z.string().trim().min(1, { error: "Vui lòng chọn thửa đất." }),
      cropId: z.string().trim().min(1, { error: "Vui lòng chọn cây trồng." }),
      startDate: z.string().trim().min(1, { error: "Vui lòng nhập ngày bắt đầu." }),
      expectedHarvestDate: z.string().trim().min(1, { error: "Vui lòng nhập ngày dự kiến thu hoạch." }),
      targetYield: z
        .string()
        .trim()
        .min(1, { error: "Vui lòng nhập mục tiêu sản lượng.", abort: true })
        .refine((text) => parseDecimalInput(text) !== null, {
          error: "Mục tiêu sản lượng phải là số, ví dụ 4500 hoặc 4500,5.",
          abort: true,
        })
        .refine((text) => (parseDecimalInput(text) ?? 0) > 0, {
          error: "Mục tiêu sản lượng phải lớn hơn 0.",
          abort: true,
        })
        .refine((text) => YIELD_PATTERN.test(text), {
          error: "Mục tiêu sản lượng tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.",
        }),
    })
    .superRefine((data, ctx) => {
      if (data.startDate && data.expectedHarvestDate && data.expectedHarvestDate < data.startDate) {
        ctx.addIssue({
          code: z.ZodIssueCode.custom,
          message: "Ngày dự kiến thu hoạch phải lớn hơn hoặc bằng ngày bắt đầu.",
          path: ["expectedHarvestDate"],
        });
      }
    });
}

/** Giá trị trong ô nhập form (chuỗi). */
export type SeasonFormValues = z.input<ReturnType<typeof seasonFormSchema>>;
/** Giá trị sau khi kiểm tra. */
export type SeasonFormOutput = z.output<ReturnType<typeof seasonFormSchema>>;

/** Khởi tạo giá trị form tạo mới với ngày bắt đầu mặc định là hôm nay. */
export function getEmptySeasonFormValues(): SeasonFormValues {
  return {
    plotId: "",
    cropId: "",
    startDate: todayInVietnam(),
    expectedHarvestDate: "",
    targetYield: "",
  };
}

/** Điền form sửa từ dữ liệu mùa vụ đang có. */
export function seasonToFormValues(season: SeasonResponse): SeasonFormValues {
  return {
    plotId: season.plotId,
    cropId: season.cropId,
    startDate: season.startDate,
    expectedHarvestDate: season.expectedHarvestDate,
    targetYield: season.targetYield === null || season.targetYield === undefined ? "" : String(season.targetYield).replace(".", ","),
  };
}

export function toCreateSeasonRequest(values: SeasonFormOutput): CreateSeasonRequest {
  return {
    plotId: values.plotId,
    cropId: values.cropId,
    startDate: values.startDate,
    expectedHarvestDate: values.expectedHarvestDate,
    targetYield: parseDecimalInput(values.targetYield) ?? 0,
  };
}

export function toUpdateSeasonRequest(values: SeasonFormOutput, version: number): UpdateSeasonRequest {
  return {
    plotId: values.plotId,
    cropId: values.cropId,
    startDate: values.startDate,
    expectedHarvestDate: values.expectedHarvestDate,
    targetYield: parseDecimalInput(values.targetYield) ?? 0,
    version,
  };
}

const formFields = [
  "plotId",
  "cropId",
  "startDate",
  "expectedHarvestDate",
  "targetYield",
] as const satisfies readonly (keyof SeasonFormValues)[];

/** Gắn lỗi xác thực từ backend vào từng ô tương ứng trên form. */
export function setSeasonFieldErrors(error: unknown, setError: UseFormSetError<SeasonFormValues>): boolean {
  return setFieldErrors(error, setError, formFields);
}

// Cột numeric(18,3): tối đa 15 chữ số phần nguyên, 3 chữ số sau dấu phẩy (BR-SEA-09).
const QUANTITY_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;

/** Form khai báo vật tư dự kiến cho mùa vụ (FR-SEA-04, BR-SEA-09). */
export function seasonSupplyPlanSchema() {
  return z.object({
    supplyId: z.string().trim().min(1, { error: "Vui lòng chọn vật tư." }),
    plannedQuantity: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập lượng dự kiến.", abort: true })
      .refine((text) => parseDecimalInput(text) !== null, {
        error: "Lượng dự kiến phải là số, ví dụ 50 hoặc 50,5.",
        abort: true,
      })
      .refine((text) => (parseDecimalInput(text) ?? 0) > 0, {
        error: "Lượng dự kiến phải lớn hơn 0.",
        abort: true,
      })
      .refine((text) => QUANTITY_PATTERN.test(text), {
        error: "Lượng dự kiến tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.",
      }),
  });
}

export type SeasonSupplyPlanFormValues = z.input<ReturnType<typeof seasonSupplyPlanSchema>>;
export type SeasonSupplyPlanFormOutput = z.output<ReturnType<typeof seasonSupplyPlanSchema>>;

/** Form điều chỉnh lượng vật tư dự kiến của mùa vụ (FR-SEA-04). */
export function editSeasonSupplyPlanSchema() {
  return z.object({
    plannedQuantity: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập lượng dự kiến.", abort: true })
      .refine((text) => parseDecimalInput(text) !== null, {
        error: "Lượng dự kiến phải là số, ví dụ 50 hoặc 50,5.",
        abort: true,
      })
      .refine((text) => (parseDecimalInput(text) ?? 0) > 0, {
        error: "Lượng dự kiến phải lớn hơn 0.",
        abort: true,
      })
      .refine((text) => QUANTITY_PATTERN.test(text), {
        error: "Lượng dự kiến tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.",
      }),
  });
}

export type EditSeasonSupplyPlanFormValues = z.input<ReturnType<typeof editSeasonSupplyPlanSchema>>;
export type EditSeasonSupplyPlanFormOutput = z.output<ReturnType<typeof editSeasonSupplyPlanSchema>>;

const supplyPlanFields = [
  "supplyId",
  "plannedQuantity",
] as const satisfies readonly (keyof SeasonSupplyPlanFormValues)[];

export function setSupplyPlanFieldErrors(
  error: unknown,
  setError: UseFormSetError<SeasonSupplyPlanFormValues>,
): boolean {
  return setFieldErrors(error, setError, supplyPlanFields);
}

const editSupplyPlanFields = [
  "plannedQuantity",
] as const satisfies readonly (keyof EditSeasonSupplyPlanFormValues)[];

export function setEditSupplyPlanFieldErrors(
  error: unknown,
  setError: UseFormSetError<EditSeasonSupplyPlanFormValues>,
): boolean {
  return setFieldErrors(error, setError, editSupplyPlanFields);
}
