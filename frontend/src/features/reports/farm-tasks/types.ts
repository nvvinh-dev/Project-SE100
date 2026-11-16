import type { ReportFilter } from "@/features/reports/common/types";

export type FarmTaskReportGroupBy = "Time" | "Assignee";

export interface FarmTaskReportQuery extends ReportFilter {
  groupBy?: FarmTaskReportGroupBy;
}

export interface FarmTaskReportOverview {
  plannedCount: number;
  completedCount: number;
  overdueCount: number;
  pendingCount: number;
  cancelledCount: number;
}

export interface FarmTaskReportRow {
  key: string;
  label: string;
  isUnassigned: boolean;
  plannedCount: number;
  completedCount: number;
  overdueCount: number;
  pendingCount: number;
}

export interface FarmTaskReportResponse {
  groupBy: FarmTaskReportGroupBy;
  overview: FarmTaskReportOverview;
  rows: FarmTaskReportRow[];
}
