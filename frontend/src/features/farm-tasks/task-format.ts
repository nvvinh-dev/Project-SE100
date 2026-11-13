import type { FarmTaskType } from "@/lib/enums";
import { farmTaskTypeLabels } from "@/lib/enum-labels";

export function formatFarmTaskTitle(task: { type: FarmTaskType; typeOther: string | null }): string {
  if (task.type === "Other" && task.typeOther) {
    return task.typeOther;
  }
  return farmTaskTypeLabels[task.type];
}
