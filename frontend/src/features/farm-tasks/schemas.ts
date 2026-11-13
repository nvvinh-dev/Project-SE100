import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type {
  CreateFarmTaskRequest,
  CreateRecurringFarmTasksRequest,
  FarmTaskResponse,
  UpdateFarmTaskRequest,
} from "@/features/farm-tasks/types";
import { todayInVietnam } from "@/features/seasons/schemas";
import { farmTaskTypes } from "@/lib/enums";
import { setFieldErrors } from "@/lib/form-errors";
import { parseDecimalInput } from "@/lib/format";

const QUANTITY_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;
const POSITIVE_INT_PATTERN = /^\d+$/;

const plannedSupplyRowSchema = z.object({
  supplyId: z.string().trim().min(1, { error: "Vui lòng chọn vật tư." }),
  plannedQuantity: z
    .string()
    .trim()
    .min(1, { error: "Vui lòng nhập lượng dự kiến.", abort: true })
    .refine((text) => parseDecimalInput(text) !== null, {
      error: "Lượng vật tư phải là số, ví dụ 5 hoặc 2,5.",
      abort: true,
    })
    .refine((text) => (parseDecimalInput(text) ?? 0) > 0, {
      error: "Lượng vật tư phải lớn hơn 0.",
      abort: true,
    })
    .refine((text) => QUANTITY_PATTERN.test(text), {
      error: "Tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.",
    }),
});

export function farmTaskFormSchema() {
  return z
    .object({
      seasonId: z.string().trim().min(1, { error: "Vui lòng chọn mùa vụ." }),
      type: z.enum(farmTaskTypes, { error: "Vui lòng chọn loại công việc." }),
      typeOther: z.string().trim().max(200, { error: "Tên loại công việc tối đa 200 ký tự." }),
      isRecurring: z.boolean(),
      scheduledDate: z.string().trim().min(1, { error: "Vui lòng chọn ngày dự kiến." }),
      intervalDays: z.string().trim(),
      assigneeId: z.string().trim(),
      plannedDurationMinutes: z.string().trim(),
      note: z.string().trim().max(2000, { error: "Ghi chú tối đa 2000 ký tự." }),
      supplies: z.array(plannedSupplyRowSchema),
    })
    .superRefine((data, ctx) => {
      if (data.type === "Other" && data.typeOther.length === 0) {
        ctx.addIssue({
          code: "custom",
          message: "Vui lòng nhập tên loại công việc.",
          path: ["typeOther"],
        });
      }

      if (data.isRecurring) {
        if (!POSITIVE_INT_PATTERN.test(data.intervalDays) || Number(data.intervalDays) < 1) {
          ctx.addIssue({
            code: "custom",
            message: "Chu kỳ lặp phải là số nguyên tối thiểu 1 ngày.",
            path: ["intervalDays"],
          });
        }
      }

      if (data.plannedDurationMinutes.length > 0) {
        if (!POSITIVE_INT_PATTERN.test(data.plannedDurationMinutes) || Number(data.plannedDurationMinutes) <= 0) {
          ctx.addIssue({
            code: "custom",
            message: "Thời lượng dự kiến phải là số nguyên dương (phút).",
            path: ["plannedDurationMinutes"],
          });
        }
      }

      const seen = new Map<string, number>();
      data.supplies.forEach((row, index) => {
        if (!row.supplyId) {
          return;
        }
        if (seen.has(row.supplyId)) {
          ctx.addIssue({
            code: "custom",
            message: "Mỗi vật tư chỉ được khai báo một dòng.",
            path: ["supplies", index, "supplyId"],
          });
        } else {
          seen.set(row.supplyId, index);
        }
      });
    });
}

export type FarmTaskFormValues = z.input<ReturnType<typeof farmTaskFormSchema>>;
export type FarmTaskFormOutput = z.output<ReturnType<typeof farmTaskFormSchema>>;

export function getEmptyFarmTaskFormValues(seasonId = ""): FarmTaskFormValues {
  return {
    seasonId,
    type: "Watering",
    typeOther: "",
    isRecurring: false,
    scheduledDate: todayInVietnam(),
    intervalDays: "7",
    assigneeId: "",
    plannedDurationMinutes: "",
    note: "",
    supplies: [],
  };
}

export function farmTaskToFormValues(task: FarmTaskResponse): FarmTaskFormValues {
  return {
    seasonId: task.seasonId,
    type: task.type,
    typeOther: task.typeOther ?? "",
    isRecurring: false,
    scheduledDate: task.scheduledDate,
    intervalDays: "7",
    assigneeId: task.assigneeId ?? "",
    plannedDurationMinutes: task.plannedDurationMinutes !== null ? String(task.plannedDurationMinutes) : "",
    note: task.note ?? "",
    supplies: task.supplyUsages.map((u) => ({
      supplyId: u.supplyId,
      plannedQuantity: u.plannedQuantity !== null ? String(u.plannedQuantity).replace(".", ",") : "",
    })),
  };
}

function mapPlannedSupplies(values: FarmTaskFormOutput) {
  return values.supplies.map((s) => ({
    supplyId: s.supplyId,
    plannedQuantity: parseDecimalInput(s.plannedQuantity) ?? 0,
  }));
}

export function toCreateFarmTaskRequest(values: FarmTaskFormOutput): CreateFarmTaskRequest {
  return {
    seasonId: values.seasonId,
    type: values.type,
    typeOther: values.type === "Other" ? values.typeOther : null,
    scheduledDate: values.scheduledDate,
    assigneeId: values.assigneeId || null,
    plannedDurationMinutes: values.plannedDurationMinutes ? Number(values.plannedDurationMinutes) : null,
    note: values.note || null,
    supplies: mapPlannedSupplies(values),
  };
}

export function toCreateRecurringFarmTasksRequest(values: FarmTaskFormOutput): CreateRecurringFarmTasksRequest {
  return {
    seasonId: values.seasonId,
    type: values.type,
    typeOther: values.type === "Other" ? values.typeOther : null,
    firstScheduledDate: values.scheduledDate,
    intervalDays: Number(values.intervalDays),
    assigneeId: values.assigneeId || null,
    plannedDurationMinutes: values.plannedDurationMinutes ? Number(values.plannedDurationMinutes) : null,
    note: values.note || null,
    supplies: mapPlannedSupplies(values),
  };
}

export function toUpdateFarmTaskRequest(values: FarmTaskFormOutput, version: number): UpdateFarmTaskRequest {
  return {
    type: values.type,
    typeOther: values.type === "Other" ? values.typeOther : null,
    scheduledDate: values.scheduledDate,
    assigneeId: values.assigneeId || null,
    plannedDurationMinutes: values.plannedDurationMinutes ? Number(values.plannedDurationMinutes) : null,
    note: values.note || null,
    supplies: mapPlannedSupplies(values),
    version,
  };
}

export function applyFarmTaskFormServerErrors(error: unknown, setError: UseFormSetError<FarmTaskFormValues>): boolean {
  return setFieldErrors(error, setError, [
    "seasonId",
    "type",
    "typeOther",
    "scheduledDate",
    "intervalDays",
    "assigneeId",
    "plannedDurationMinutes",
    "note",
  ]);
}
