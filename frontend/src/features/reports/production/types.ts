import type { ReportFilter, ReportSeasonSummary } from "@/features/reports/common/types";
import type { Unit } from "@/lib/enums";

export type PreviousSeasonCompareBy = "SamePlot" | "SameCrop";

export interface ProductionReportQuery extends ReportFilter {
  deviationThreshold?: number;
  onlyHighDeviation?: boolean;
  compareBy?: PreviousSeasonCompareBy;
}

export interface ProductionOverview {
  inProgressCount: number;
  closedCount: number;
  plannedCount: number;
  cancelledCount: number;
  totalCultivatedAreaM2: number;
}

export interface ProductionSeasonComparison {
  previousSeasonId: string | null;
  previousSeasonLabel: string | null;
  previousStartDate: string | null;
  previousYieldUnit: Unit | null;
  previousYieldPerHectare: number | null;
  yieldPerHectareDelta: number | null;
  sameYieldUnit: boolean;
  previousSupplyCost: number | null;
  supplyCostDelta: number | null;
  supplyCostPerM2: number | null;
  previousSupplyCostPerM2: number | null;
  supplyCostPerM2Delta: number | null;
  previousRevenue: number | null;
  revenueDelta: number | null;
  revenuePerM2: number | null;
  previousRevenuePerM2: number | null;
  revenuePerM2Delta: number | null;
  message: string | null;
}

export interface ProductionSeasonItem {
  season: ReportSeasonSummary;
  actualYield: number;
  targetYield: number;
  yieldDeviation: number;
  isHighDeviation: boolean;
  yieldPerM2: number;
  yieldPerHectare: number;
  supplyCost: number | null;
  revenue: number | null;
  profit: number | null;
  previousComparison: ProductionSeasonComparison | null;
}

export interface ProductionReportResponse {
  overview: ProductionOverview;
  deviationThreshold: number;
  compareBy: PreviousSeasonCompareBy;
  includesFinancialColumns: boolean;
  seasons: ProductionSeasonItem[];
  cancelledSeasons: ProductionSeasonItem[];
}
