import type { UseFormSetError } from "react-hook-form";
import { z } from "zod";
import type {
  FarmTaskActualSupplyInput,
  FarmTaskDetail,
  FarmTaskResponse,
  RecordFarmTaskResultRequest,
} from "@/features/farm-tasks/types";
import { todayInVietnam } from "@/features/seasons/schemas";
import { setFieldErrors } from "@/lib/form-errors";
import { parseDecimalInput } from "@/lib/format";

const QUANTITY_PATTERN = /^\d{1,15}(?:[.,]\d{1,3})?$/;
const POSITIVE_INT_PATTERN = /^\d+$/;

const actualSupplyRowSchema = z.object({
  supplyId: z.string().trim().min(1, { error: "Vui lòng chọn vật tư." }),
  actualQuantity: z.string().trim(),
  plannedQuantity: z.number().nullable(),
  initialActualQuantity: z.number().nullable(),
  initialStockTransactionId: z.string().nullable(),
});

export function recordResultFormSchema(isWorker: boolean) {
  return z
    .object({
      status: z.enum(["Done", "WaitingForSupply"]),
      performedDate: z.string().trim(),
      actualDurationMinutes: z.string().trim(),
      abnormalNote: z.string().trim().max(2000, { error: "Ghi chú bất thường tối đa 2000 ký tự." }),
      fieldNote: z.string().trim().max(2000, { error: "Ghi chú hiện trường tối đa 2000 ký tự." }),
      supplies: z.array(actualSupplyRowSchema),
    })
    .superRefine((data, ctx) => {
      if (isWorker && data.status !== "Done") {
        ctx.addIssue({
          code: "custom",
          message: "Nhân công chỉ được xác nhận hoàn thành.",
          path: ["status"],
        });
      }

      if (data.status === "Done" && data.performedDate.length === 0) {
        ctx.addIssue({
          code: "custom",
          message: "Vui lòng chọn ngày thực hiện.",
          path: ["performedDate"],
        });
      }

      if (data.performedDate.length > 0 && data.performedDate > todayInVietnam()) {
        ctx.addIssue({
          code: "custom",
          message: "Ngày thực hiện không được ở tương lai.",
          path: ["performedDate"],
        });
      }

      if (data.actualDurationMinutes.length > 0) {
        if (!POSITIVE_INT_PATTERN.test(data.actualDurationMinutes) || Number(data.actualDurationMinutes) <= 0) {
          ctx.addIssue({
            code: "custom",
            message: "Thời lượng thực tế phải là số nguyên dương (phút).",
            path: ["actualDurationMinutes"],
          });
        }
      }

      if (isWorker) {
        return;
      }

      const seen = new Map<string, number>();
      data.supplies.forEach((row, index) => {
        if (row.supplyId) {
          if (seen.has(row.supplyId)) {
            ctx.addIssue({
              code: "custom",
              message: "Mỗi vật tư chỉ được khai báo một dòng.",
              path: ["supplies", index, "supplyId"],
            });
          } else {
            seen.set(row.supplyId, index);
          }
        }

        if (data.status === "Done" && row.actualQuantity.length > 0) {
          const parsed = parseDecimalInput(row.actualQuantity);
          if (parsed === null) {
            ctx.addIssue({
              code: "custom",
              message: "Lượng vật tư phải là số, ví dụ 5 hoặc 2,5.",
              path: ["supplies", index, "actualQuantity"],
            });
          } else if (parsed <= 0) {
            ctx.addIssue({
              code: "custom",
              message: "Lượng thực dùng phải lớn hơn 0 (hoặc để trống nếu không dùng).",
              path: ["supplies", index, "actualQuantity"],
            });
          } else if (!QUANTITY_PATTERN.test(row.actualQuantity)) {
            ctx.addIssue({
              code: "custom",
              message: "Tối đa 3 chữ số sau dấu phẩy và 15 chữ số phần nguyên.",
              path: ["supplies", index, "actualQuantity"],
            });
          }
        }
      });
    });
}

export type RecordResultFormValues = z.input<ReturnType<typeof recordResultFormSchema>>;
export type RecordResultFormOutput = z.output<ReturnType<typeof recordResultFormSchema>>;

function isHouseholdTask(task: FarmTaskDetail): task is FarmTaskResponse {
  return "createdById" in task;
}

function formatNumberInput(value: number | null): string {
  return value === null ? "" : String(value).replace(".", ",");
}

export function farmTaskToResultFormValues(task: FarmTaskDetail, isWorker: boolean): RecordResultFormValues {
  const householdTask = isHouseholdTask(task) ? task : null;
  const supplies =
    !isWorker && householdTask
      ? householdTask.supplyUsages.map((u) => {
          const prefillQty =
            u.actualQuantity !== null
              ? u.actualQuantity
              : task.status !== "Done"
                ? u.plannedQuantity
                : null;
          return {
            supplyId: u.supplyId,
            actualQuantity: formatNumberInput(prefillQty),
            plannedQuantity: u.plannedQuantity,
            initialActualQuantity: u.actualQuantity,
            initialStockTransactionId: u.stockTransactionId,
          };
        })
      : [];

  return {
    status: "Done",
    performedDate: task.performedDate ?? todayInVietnam(),
    actualDurationMinutes: task.actualDurationMinutes !== null ? String(task.actualDurationMinutes) : "",
    abnormalNote: task.abnormalNote ?? "",
    fieldNote: task.fieldNote ?? "",
    supplies,
  };
}

export function hasSupplyAdjustmentRequiringReason(
  task: FarmTaskDetail,
  supplies: readonly RecordResultFormOutput["supplies"][number][],
): boolean {
  if (!isHouseholdTask(task)) {
    return false;
  }

  const bySupplyId = new Map(supplies.map((row) => [row.supplyId, row]));

  return task.supplyUsages.some((original) => {
    if (original.stockTransactionId === null) {
      return false;
    }
    const edited = bySupplyId.get(original.supplyId);
    if (!edited) {
      return true;
    }
    const parsed = edited.actualQuantity ? parseDecimalInput(edited.actualQuantity) : null;
    return parsed === null || parsed <= 0 || parsed !== original.actualQuantity;
  });
}

export function toRecordFarmTaskResultRequest(
  values: RecordResultFormOutput,
  isWorker: boolean,
  version: number,
  reason: string | null,
): RecordFarmTaskResultRequest {
  const isWaiting = !isWorker && values.status === "WaitingForSupply";

  let supplies: FarmTaskActualSupplyInput[] | null = null;
  if (!isWorker) {
    if (isWaiting) {
      supplies = values.supplies
        .filter((row) => row.supplyId && (row.plannedQuantity !== null || row.initialStockTransactionId !== null))
        .map((row) => ({ supplyId: row.supplyId, actualQuantity: null }));
    } else {
      supplies = values.supplies
        .filter((row) => row.supplyId)
        .map((row) => {
          const parsed = row.actualQuantity ? parseDecimalInput(row.actualQuantity) : null;
          return {
            supplyId: row.supplyId,
            actualQuantity: parsed && parsed > 0 ? parsed : null,
          };
        });
    }
  }

  return {
    status: isWorker ? "Done" : values.status,
    performedDate: isWaiting ? null : values.performedDate || null,
    actualDurationMinutes: values.actualDurationMinutes ? Number(values.actualDurationMinutes) : null,
    abnormalNote: values.abnormalNote || null,
    fieldNote: values.fieldNote || null,
    supplies,
    reason,
    version,
  };
}

export function applyRecordResultServerErrors(
  error: unknown,
  setError: UseFormSetError<RecordResultFormValues>,
): boolean {
  return setFieldErrors(error, setError, [
    "status",
    "performedDate",
    "actualDurationMinutes",
    "abnormalNote",
    "fieldNote",
  ]);
}
