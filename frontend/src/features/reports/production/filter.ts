import { applyFilterParams, readReportFilter } from "@/features/reports/common/filter";
import type { PreviousSeasonCompareBy, ProductionReportQuery } from "@/features/reports/production/types";

export const DEFAULT_DEVIATION_THRESHOLD = 20;

export function readProductionReportQuery(params: URLSearchParams): ProductionReportQuery {
  const base = readReportFilter(params);
  const rawThreshold = params.get("deviationThreshold");
  const parsedThreshold = rawThreshold ? Number(rawThreshold) : NaN;
  const deviationThreshold =
    Number.isFinite(parsedThreshold) && parsedThreshold > 0
      ? parsedThreshold
      : DEFAULT_DEVIATION_THRESHOLD;

  const onlyHighDeviation = params.get("onlyHighDeviation") === "true";
  const rawCompareBy = params.get("compareBy");
  const compareBy: PreviousSeasonCompareBy = rawCompareBy === "SameCrop" ? "SameCrop" : "SamePlot";

  return {
    ...base,
    deviationThreshold,
    onlyHighDeviation,
    compareBy,
  };
}

export function applyProductionFilterParams(
  params: URLSearchParams,
  changes: Partial<{
    from: string | undefined;
    to: string | undefined;
    seasonId: string | undefined;
    plotId: string | undefined;
    cropId: string | undefined;
    deviationThreshold: number | undefined;
    onlyHighDeviation: boolean | undefined;
    compareBy: PreviousSeasonCompareBy | undefined;
  }>,
): URLSearchParams {
  const mapped: Record<string, string | undefined> = {};

  if ("from" in changes) mapped.from = changes.from;
  if ("to" in changes) mapped.to = changes.to;
  if ("seasonId" in changes) mapped.seasonId = changes.seasonId;
  if ("plotId" in changes) mapped.plotId = changes.plotId;
  if ("cropId" in changes) mapped.cropId = changes.cropId;

  if ("deviationThreshold" in changes) {
    const val = changes.deviationThreshold;
    mapped.deviationThreshold =
      val !== undefined && val !== DEFAULT_DEVIATION_THRESHOLD ? String(val) : undefined;
  }

  if ("onlyHighDeviation" in changes) {
    mapped.onlyHighDeviation = changes.onlyHighDeviation ? "true" : undefined;
  }

  if ("compareBy" in changes) {
    mapped.compareBy = changes.compareBy === "SameCrop" ? "SameCrop" : undefined;
  }

  return applyFilterParams(params, mapped);
}
