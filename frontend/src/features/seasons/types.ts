import type { PageQuery } from "@/lib/api-client";
import type { QualityGrade, SeasonStatus, Unit, UserRole } from "@/lib/enums";

// Loại thao tác mùa vụ theo quy tắc trạng thái (BR-SEA-10).
export const seasonOperations = [
  "EditScheduleAndYield",
  "ChangePlotOrCrop",
  "ManagePlannedSupplies",
  "ManageAssignments",
  "ManageFarmTasks",
  "RecordFarmTaskResult",
  "StockOut",
  "ManageHarvests",
  "AdjustProduceStock",
  "ManageSales",
  "RecordPayment",
  "ManageExpenses",
  "ManageNotes",
  "GenerateAISummary",
] as const;

export type SeasonOperation = (typeof seasonOperations)[number];

// Chi tiết một mùa vụ (FR-SEA-01, FR-SEA-12).
export interface SeasonResponse {
  id: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  yieldUnit: Unit;
  startDate: string;
  expectedHarvestDate: string;
  targetYield: number;
  status: SeasonStatus;
  version: number;
}

// Dữ liệu tạo mùa vụ mới (FR-SEA-01).
export interface CreateSeasonRequest {
  plotId: string;
  cropId: string;
  startDate: string;
  expectedHarvestDate: string;
  targetYield: number;
}

// Dữ liệu sửa mùa vụ (FR-SEA-07, BR-GEN-11).
export interface UpdateSeasonRequest {
  plotId: string;
  cropId: string;
  startDate: string;
  expectedHarvestDate: string;
  targetYield: number;
  version: number;
}

// Thông tin rút gọn của cây trồng dùng cho ô chọn cây (FR-SEA-01, FR-SEA-02).
export interface CropOptionItem {
  id: string;
  name: string;
  yieldUnit: Unit;
  status?: string;
}

// Một dòng trong danh sách mùa vụ (FR-SEA-11).
export interface SeasonListItem {
  id: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  yieldUnit: Unit;
  startDate: string;
  expectedHarvestDate: string;
  targetYield: number;
  status: SeasonStatus;
}

// Tham số danh sách mùa vụ; sort: startDate (mặc định), expectedHarvestDate, targetYield, createdAt (FR-SEA-11).
export interface SeasonQuery extends PageQuery {
  plotId?: string;
  cropId?: string;
  status?: SeasonStatus;
  from?: string;
  to?: string;
}

// Tham số truy vấn ô chọn mùa vụ theo loại thao tác (FR-SEA-11).
export interface SeasonOptionQuery {
  operation: SeasonOperation;
}

// Mục lựa chọn mùa vụ cho component SeasonSelect dùng chung (FR-SEA-11).
export interface SeasonOptionItem {
  id: string;
  label: string;
  status: SeasonStatus;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  yieldUnit: Unit;
  startDate: string;
  expectedHarvestDate: string;
}

// Tồn nông sản theo loại chất lượng của mùa vụ (FR-SEA-12).
export interface ProduceStockSummaryItem {
  grade: QualityGrade;
  quantity: number;
  unit: Unit;
}

// Tóm tắt tài chính của mùa vụ (FR-SEA-12, BR-USR-13).
export interface SeasonFinancialSummary {
  totalExpense: number;
  totalRevenue: number;
  estimatedProfit: number;
}

// Người được phân công vào mùa vụ (FR-SEA-06, FR-SEA-12).
export interface SeasonAssignmentItem {
  id: string;
  seasonId: string;
  userId: string;
  fullName: string;
  role: UserRole;
  phoneNumber: string;
  createdAt: string;
}

// Chi tiết mùa vụ đầy đủ (FR-SEA-12).
export interface SeasonDetailResponse {
  id: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  yieldUnit: Unit;
  startDate: string;
  expectedHarvestDate: string;
  targetYield: number;
  status: SeasonStatus;
  startedAt?: string;
  closedAt?: string;
  cancelledAt?: string;
  cancelReason?: string;
  version: number;
  harvestedYield: number;
  completedTasksCount: number;
  totalTasksCount: number;
  produceStocks: ProduceStockSummaryItem[];
  financialSummary?: SeasonFinancialSummary;
  assignments: SeasonAssignmentItem[];
  isOutOfScope: boolean;
  canEdit: boolean;
  canStart: boolean;
  canClose: boolean;
  canCancel: boolean;
  canReopen: boolean;
}

// Yêu cầu phân công người vào mùa vụ (FR-SEA-06).
export interface CreateSeasonAssignmentRequest {
  seasonId: string;
  userId: string;
}

// Yêu cầu thu hồi phân công (FR-SEA-06, BR-USR-12, BR-GEN-14).
export interface UnassignSeasonAssignmentRequest {
  confirmed?: boolean;
}

// Yêu cầu chốt mùa vụ (FR-SEA-09, BR-SEA-05, BR-GEN-14).
export interface CloseSeasonRequest {
  confirmed?: boolean;
}

// Yêu cầu hủy mùa vụ (FR-SEA-10, BR-SEA-06, BR-GEN-06, BR-GEN-14).
export interface CancelSeasonRequest {
  reason: string;
  confirmed?: boolean;
}

// Yêu cầu mở lại mùa vụ đã chốt (FR-SEA-14, BR-SEA-08, BR-GEN-06).
export interface ReopenSeasonRequest {
  reason: string;
}

// Ghi chú kinh nghiệm mùa vụ (FR-SEA-13).
export interface SeasonNoteResponse {
  id: string;
  seasonId: string;
  content: string;
  createdById: string;
  authorName: string;
  createdAt: string;
  updatedAt?: string;
  version: number;
  canEdit: boolean;
}

// Yêu cầu tạo ghi chú kinh nghiệm (FR-SEA-13, BR-SEA-11).
export interface CreateSeasonNoteRequest {
  seasonId: string;
  content: string;
}

// Yêu cầu sửa ghi chú kinh nghiệm (FR-SEA-13, BR-SEA-11).
export interface UpdateSeasonNoteRequest {
  content: string;
  version: number;
}

// Thành viên hộ để chọn phân công.
export interface HouseholdMemberOption {
  id: string;
  fullName: string;
  role: UserRole;
  phoneNumber?: string;
  isLocked?: boolean;
}

// Dòng kế hoạch vật tư dự kiến của mùa vụ (FR-SEA-04, BR-SEA-09).
export interface SeasonSupplyPlanResponse {
  id: string;
  seasonId: string;
  supplyId: string;
  supplyName: string;
  unit: Unit;
  plannedQuantity: number;
  currentStock: number;
  minStockLevel: number;
  warningCode?: "SUPPLY_SHORTAGE" | "LOW_STOCK" | null;
  version: number;
}

// Yêu cầu khai báo vật tư dự kiến cho mùa vụ (FR-SEA-04, BR-SEA-10).
export interface CreateSeasonSupplyPlanRequest {
  seasonId: string;
  supplyId: string;
  plannedQuantity: number;
}

// Yêu cầu cập nhật lượng vật tư dự kiến của mùa vụ (FR-SEA-04, BR-SEA-10).
export interface UpdateSeasonSupplyPlanRequest {
  plannedQuantity: number;
  version: number;
}
