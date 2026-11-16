import { applyFilterParams, readReportFilter } from "@/features/reports/common/filter";
import type { FarmTaskReportGroupBy, FarmTaskReportQuery } from "@/features/reports/farm-tasks/types";

export function readFarmTaskReportQuery(params: URLSearchParams): FarmTaskReportQuery {
  const base = readReportFilter(params);
  const groupBy: FarmTaskReportGroupBy = params.get("groupBy") === "Assignee" ? "Assignee" : "Time";
  return { ...base, groupBy };
}

export function applyFarmTaskFilterParams(
  params: URLSearchParams,
  changes: Partial<{
    from: string | undefined;
    to: string | undefined;
    seasonId: string | undefined;
    plotId: string | undefined;
    cropId: string | undefined;
    groupBy: FarmTaskReportGroupBy | undefined;
  }>,
): URLSearchParams {
  const mapped: Record<string, string | undefined> = {};
  if ("from" in changes) mapped.from = changes.from;
  if ("to" in changes) mapped.to = changes.to;
  if ("seasonId" in changes) mapped.seasonId = changes.seasonId;
  if ("plotId" in changes) mapped.plotId = changes.plotId;
  if ("cropId" in changes) mapped.cropId = changes.cropId;
  if ("groupBy" in changes) {
    mapped.groupBy = changes.groupBy === "Assignee" ? "Assignee" : undefined;
  }
  return applyFilterParams(params, mapped);
}
