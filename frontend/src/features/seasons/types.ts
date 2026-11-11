import type { PageQuery } from "@/lib/api-client";
import type { SeasonStatus, Unit } from "@/lib/enums";

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
