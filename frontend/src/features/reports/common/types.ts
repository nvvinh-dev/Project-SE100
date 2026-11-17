import type { ExportFormat, SeasonStatus, Unit } from "@/lib/enums";

/** Bộ lọc dùng chung cho mọi màn hình báo cáo (FR-RPT-01). */
export interface ReportFilter {
  from?: string;
  to?: string;
  seasonId?: string;
  plotId?: string;
  cropId?: string;
}

/** Thông tin tóm tắt mùa vụ trong báo cáo (FR-RPT-03, BR-RPT-03). */
export interface ReportSeasonSummary {
  seasonId: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  yieldUnit: Unit;
  areaM2: number;
  startDate: string;
  expectedHarvestDate: string;
  status: SeasonStatus;
  isProvisional: boolean;
}

/** Yêu cầu gửi lên endpoint xuất báo cáo (FR-RPT-04, FR-RPT-05). */
export interface ExportReportRequest extends ReportFilter {
  format: ExportFormat;
}
