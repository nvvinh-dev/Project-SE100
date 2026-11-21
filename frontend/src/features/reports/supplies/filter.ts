import { applyFilterParams } from "@/features/reports/common/filter";
import type { SupplyReportQuery } from "@/features/reports/supplies/types";
import { type SupplyGroup, supplyGroups } from "@/lib/enums";

function isSupplyGroup(value: string | null): value is SupplyGroup {
  return value !== null && (supplyGroups as readonly string[]).includes(value);
}

/** Đọc bộ lọc báo cáo vật tư từ đường dẫn; nhóm không hợp lệ thì bỏ qua. */
export function readSupplyReportQuery(params: URLSearchParams): SupplyReportQuery {
  const group = params.get("group");
  return {
    from: params.get("from") || undefined,
    to: params.get("to") || undefined,
    group: isSupplyGroup(group) ? group : undefined,
  };
}

export function applySupplyReportParams(params: URLSearchParams, changes: Partial<SupplyReportQuery>): URLSearchParams {
  const mapped: Record<string, string | undefined> = {};
  if ("from" in changes) mapped.from = changes.from;
  if ("to" in changes) mapped.to = changes.to;
  if ("group" in changes) mapped.group = changes.group;
  return applyFilterParams(params, mapped);
}
