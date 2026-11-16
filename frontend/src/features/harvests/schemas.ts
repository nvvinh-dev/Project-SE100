import { z } from "zod";
import type { CorrectHarvestRecordRequest, CreateHarvestRecordRequest, HarvestRecordResponse } from "@/features/harvests/types";
import { todayInVietnam } from "@/features/supplies/schemas";
import { qualityGrades, type QualityGrade } from "@/lib/enums";
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
