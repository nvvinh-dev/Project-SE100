import type { PageQuery } from "@/lib/api-client";
import type { ExtraPermission, SeasonStatus, Unit } from "@/lib/enums";

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

// Tham số truy vấn ô chọn mùa vụ theo loại thao tác (FR-SEA-11, BR-SEA-10) hoặc quyền xem (BR-USR-13).
export interface SeasonOptionQuery {
  operation?: SeasonOperation;
  permission?: ExtraPermission;
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
