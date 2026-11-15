import { z } from "zod";
import type {
  CorrectHarvestRecordRequest,
  CreateHarvestRecordRequest,
  CreateProduceStockAdjustmentRequest,
  HarvestRecordResponse,
} from "@/features/harvests/types";
import { todayInVietnam } from "@/features/supplies/schemas";
import { produceAdjustmentReasons, type ProduceAdjustmentReason, qualityGrades, type QualityGrade } from "@/lib/enums";
import { parseDecimalInput } from "@/lib/format";

const NOTE_MAX = 500;
// Cột quantity trong bảng harvest_records là numeric(18,3).
const QUANTITY_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;

export const harvestFormSchema = z.object({
  seasonId: z.string().min(1, { error: "Vui lòng chọn mùa vụ." }),
  // BR-GEN-05: ngày thu hoạch không được sau hôm nay (giờ Việt Nam).
  harvestDate: z
    .string()
    .min(1, { error: "Vui lòng chọn ngày thu hoạch." })
    .refine((date) => date <= todayInVietnam(), { error: "Ngày thu hoạch không được sau hôm nay." }),
  qualityGrade: z
    .string()
    .refine((value): value is QualityGrade => (qualityGrades as readonly string[]).includes(value), {
      error: "Vui lòng chọn loại chất lượng.",
    }),
  // BR-HAR-01: sản lượng thực tế > 0.
  quantity: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập sản lượng thu hoạch." })
    .refine((text) => parseDecimalInput(text) !== null, { error: "Sản lượng phải là số hợp lệ." })
    .refine((text) => (parseDecimalInput(text) ?? 0) > 0, { error: "Sản lượng phải lớn hơn 0." })
    .refine((text) => QUANTITY_PATTERN.test(text), { error: "Sản lượng tối đa 3 chữ số sau dấu phẩy." }),
  note: z.string().trim().max(NOTE_MAX, { error: "Ghi chú tối đa 500 ký tự." }),
});

export type HarvestFormValues = z.input<typeof harvestFormSchema>;
export type HarvestFormOutput = z.output<typeof harvestFormSchema>;

export const harvestFormFields = [
  "seasonId",
  "harvestDate",
  "qualityGrade",
  "quantity",
  "note",
] as const satisfies readonly (keyof HarvestFormValues)[];

export function emptyHarvestFormValues(): HarvestFormValues {
  return {
    seasonId: "",
    harvestDate: todayInVietnam(),
    qualityGrade: "Grade1",
    quantity: "",
    note: "",
  };
}

export function harvestToFormValues(record: HarvestRecordResponse): HarvestFormValues {
  return {
    seasonId: record.seasonId,
    harvestDate: record.harvestDate,
    qualityGrade: record.qualityGrade,
    quantity: String(record.quantity).replace(".", ","),
    note: record.note ?? "",
  };
}

export function toCreateHarvestRequest(values: HarvestFormOutput, requestId: string): CreateHarvestRecordRequest {
  return {
    requestId,
    seasonId: values.seasonId,
    harvestDate: values.harvestDate,
    qualityGrade: values.qualityGrade,
    quantity: parseDecimalInput(values.quantity) ?? 0,
    note: values.note === "" ? null : values.note,
  };
}

export function toCorrectHarvestRequest(
  values: HarvestFormOutput,
  reason: string,
  version: number,
): CorrectHarvestRecordRequest {
  return {
    harvestDate: values.harvestDate,
    qualityGrade: values.qualityGrade,
    quantity: parseDecimalInput(values.quantity) ?? 0,
    note: values.note === "" ? null : values.note,
    reason,
    version,
  };
}

const REASON_OTHER_MAX = 200;

export const produceStockAdjustmentFormSchema = z
  .object({
    seasonId: z.string().min(1, { error: "Vui lòng chọn mùa vụ." }),
    qualityGrade: z
      .string()
      .refine((value): value is QualityGrade => (qualityGrades as readonly string[]).includes(value), {
        error: "Vui lòng chọn loại chất lượng.",
      }),
    // BR-HAR-05: lượng giảm > 0, cột numeric(18,3).
    quantity: z
      .string()
      .trim()
      .min(1, { error: "Vui lòng nhập lượng điều chỉnh." })
      .refine((text) => parseDecimalInput(text) !== null, { error: "Lượng điều chỉnh phải là số hợp lệ." })
      .refine((text) => (parseDecimalInput(text) ?? 0) > 0, { error: "Lượng điều chỉnh phải lớn hơn 0." })
      .refine((text) => QUANTITY_PATTERN.test(text), {
        error: "Lượng điều chỉnh tối đa 3 chữ số sau dấu phẩy.",
      }),
    // BR-GEN-05: ngày điều chỉnh không được sau hôm nay (giờ Việt Nam).
    adjustmentDate: z
      .string()
      .min(1, { error: "Vui lòng chọn ngày điều chỉnh." })
      .refine((date) => date <= todayInVietnam(), { error: "Ngày điều chỉnh không được sau hôm nay." }),
    reason: z
      .string()
      .refine(
        (value): value is ProduceAdjustmentReason => (produceAdjustmentReasons as readonly string[]).includes(value),
        { error: "Vui lòng chọn lý do điều chỉnh." },
      ),
    reasonOther: z.string().trim().max(REASON_OTHER_MAX, { error: "Lý do khác tối đa 200 ký tự." }),
    note: z.string().trim().max(NOTE_MAX, { error: "Ghi chú tối đa 500 ký tự." }),
  })
  .superRefine((data, ctx) => {
    if (data.reason === "Other" && data.reasonOther.length === 0) {
      ctx.addIssue({
        code: "custom",
        path: ["reasonOther"],
        message: "Vui lòng nhập lý do cụ thể khi chọn Khác.",
      });
    }
  });

export type ProduceStockAdjustmentFormValues = z.input<typeof produceStockAdjustmentFormSchema>;
export type ProduceStockAdjustmentFormOutput = z.output<typeof produceStockAdjustmentFormSchema>;

export const produceStockAdjustmentFormFields = [
  "seasonId",
  "qualityGrade",
  "quantity",
  "adjustmentDate",
  "reason",
  "reasonOther",
  "note",
] as const satisfies readonly (keyof ProduceStockAdjustmentFormValues)[];

export function emptyProduceStockAdjustmentFormValues(
  defaults?: { seasonId?: string; qualityGrade?: string | null },
): ProduceStockAdjustmentFormValues {
  const validGrade =
    defaults?.qualityGrade && (qualityGrades as readonly string[]).includes(defaults.qualityGrade)
      ? defaults.qualityGrade
      : "Grade1";

  return {
    seasonId: defaults?.seasonId ?? "",
    qualityGrade: validGrade,
    quantity: "",
    adjustmentDate: todayInVietnam(),
    reason: "Loss",
    reasonOther: "",
    note: "",
  };
}

export function toCreateProduceStockAdjustmentRequest(
  values: ProduceStockAdjustmentFormOutput,
  requestId: string,
): CreateProduceStockAdjustmentRequest {
  return {
    requestId,
    seasonId: values.seasonId,
    qualityGrade: values.qualityGrade,
    quantity: parseDecimalInput(values.quantity) ?? 0,
    adjustmentDate: values.adjustmentDate,
    reason: values.reason,
    reasonOther: values.reason === "Other" ? values.reasonOther : null,
    note: values.note === "" ? null : values.note,
  };
}
