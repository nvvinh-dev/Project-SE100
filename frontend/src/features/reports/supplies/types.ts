import type { ReportSeasonSummary } from "@/features/reports/common/types";
import type { RecordStatus, SupplyGroup, Unit } from "@/lib/enums";

/** Bộ lọc báo cáo vật tư (FR-RPT-01): khoảng thời gian theo ngày giao dịch và nhóm vật tư. */
export interface SupplyReportQuery {
  from?: string;
  to?: string;
  group?: SupplyGroup;
}

/** Vật tư dưới ngưỡng tại thời điểm hiện tại (FR-RPT-SUP-02, BR-SUP-12). */
export interface SupplyReportLowStockItem {
  supplyId: string;
  code: string;
  name: string;
  group: SupplyGroup;
  unit: Unit;
  stockQuantity: number;
  minStockLevel: number;
}

/** Một vật tư: lượng theo đơn vị của vật tư (BR-RPT-05). */
export interface SupplyReportRow {
  supplyId: string;
  code: string;
  name: string;
  unit: Unit;
  status: RecordStatus;
  openingQuantity: number;
  inQuantity: number;
  inValue: number;
  outQuantity: number;
  outValue: number;
  adjustmentQuantity: number;
  adjustmentValue: number;
  closingQuantity: number;
  stockQuantity: number;
  averageUnitCost: number;
  stockValue: number;
}

/** Nhóm vật tư chỉ cộng giá trị, không cộng lượng. */
export interface SupplyReportGroupItem {
  group: SupplyGroup;
  supplies: SupplyReportRow[];
  inValue: number;
  outValue: number;
  adjustmentValue: number;
  stockValue: number;
}

/** Đối chiếu xuất kho theo vật tư và mùa vụ (FR-RPT-SUP-03, BR-RPT-06). */
export interface SupplyReconciliationItem {
  supplyId: string;
  supplyCode: string;
  supplyName: string;
  unit: Unit;
  season: ReportSeasonSummary;
  totalOutQuantity: number;
  taskOutQuantity: number;
  manualOutQuantity: number;
  recordedInTasksQuantity: number;
  differenceQuantity: number;
}

export interface SupplyManualOutItem {
  stockTransactionId: string;
  transactionDate: string;
  supplyId: string;
  supplyCode: string;
  supplyName: string;
  unit: Unit;
  season: ReportSeasonSummary;
  quantity: number;
  createdByName: string;
  note: string | null;
}

export interface SupplyReportResponse {
  lowStock: SupplyReportLowStockItem[];
  groups: SupplyReportGroupItem[];
  totalStockValue: number;
  reconciliation: SupplyReconciliationItem[];
  manualOuts: SupplyManualOutItem[];
}
