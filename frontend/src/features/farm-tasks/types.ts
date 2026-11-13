import type { ResponseWarning } from "@/lib/api-client";
import type { FarmTaskStatus, FarmTaskType, Unit, UserRole } from "@/lib/enums";

export interface FarmTaskAssigneeOption {
  userId: string;
  fullName: string;
  role: UserRole;
}

export interface FarmTaskPlannedSupplyInput {
  supplyId: string;
  plannedQuantity: number;
}

export interface CreateFarmTaskRequest {
  seasonId: string;
  type: FarmTaskType;
  typeOther: string | null;
  scheduledDate: string;
  assigneeId: string | null;
  plannedDurationMinutes: number | null;
  note: string | null;
  supplies: FarmTaskPlannedSupplyInput[];
}

export interface CreateRecurringFarmTasksRequest {
  seasonId: string;
  type: FarmTaskType;
  typeOther: string | null;
  firstScheduledDate: string;
  intervalDays: number;
  assigneeId: string | null;
  plannedDurationMinutes: number | null;
  note: string | null;
  supplies: FarmTaskPlannedSupplyInput[];
}

export interface UpdateFarmTaskRequest {
  type: FarmTaskType;
  typeOther: string | null;
  scheduledDate: string;
  assigneeId: string | null;
  plannedDurationMinutes: number | null;
  note: string | null;
  supplies: FarmTaskPlannedSupplyInput[];
  version: number;
}

export interface CancelFarmTaskRequest {
  version: number;
}

export interface CancelRecurrenceGroupResponse {
  recurrenceGroupId: string;
  cancelledCount: number;
}

export interface FarmTaskActualSupplyInput {
  supplyId: string;
  actualQuantity: number | null;
}

export interface RecordFarmTaskResultRequest {
  status: FarmTaskStatus;
  performedDate: string | null;
  actualDurationMinutes: number | null;
  abnormalNote: string | null;
  fieldNote: string | null;
  supplies: FarmTaskActualSupplyInput[] | null;
  reason: string | null;
  version: number;
}

export interface RecordFarmTaskResultOutcome {
  data: FarmTaskDetail;
  warnings: ResponseWarning[];
}

export interface FarmTaskSupplyUsageItem {
  id: string;
  supplyId: string;
  supplyName: string;
  unit: Unit;
  plannedQuantity: number | null;
  actualQuantity: number | null;
  stockTransactionId: string | null;
  currentStock: number;
  minStockLevel: number;
}

export interface WorkerFarmTaskSupplyItem {
  id: string;
  supplyId: string;
  supplyName: string;
  unit: Unit;
  plannedQuantity: number | null;
}

export interface FarmTaskResponse {
  id: string;
  seasonId: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  assigneeId: string | null;
  assigneeName: string | null;
  type: FarmTaskType;
  typeOther: string | null;
  scheduledDate: string;
  plannedDurationMinutes: number | null;
  note: string | null;
  status: FarmTaskStatus;
  isOverdue: boolean;
  performedDate: string | null;
  actualDurationMinutes: number | null;
  abnormalNote: string | null;
  fieldNote: string | null;
  completedAt: string | null;
  cancelledAt: string | null;
  recurrenceGroupId: string | null;
  createdAt: string;
  createdById: string;
  createdByName: string | null;
  updatedAt: string | null;
  version: number;
  supplyUsages: FarmTaskSupplyUsageItem[];
}

export interface WorkerFarmTaskResponse {
  id: string;
  seasonId: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  assigneeId: string | null;
  assigneeName: string | null;
  type: FarmTaskType;
  typeOther: string | null;
  scheduledDate: string;
  plannedDurationMinutes: number | null;
  note: string | null;
  status: FarmTaskStatus;
  isOverdue: boolean;
  performedDate: string | null;
  actualDurationMinutes: number | null;
  abnormalNote: string | null;
  fieldNote: string | null;
  completedAt: string | null;
  updatedAt: string | null;
  version: number;
  supplyUsages: WorkerFarmTaskSupplyItem[];
}

export type FarmTaskDetail = FarmTaskResponse | WorkerFarmTaskResponse;

export interface FarmTaskListItem {
  id: string;
  seasonId: string;
  plotId: string;
  plotCode: string;
  cropId: string;
  cropName: string;
  type: FarmTaskType;
  typeOther: string | null;
  scheduledDate: string;
  plannedDurationMinutes: number | null;
  assigneeId: string | null;
  assigneeName: string | null;
  status: FarmTaskStatus;
  isOverdue: boolean;
  performedDate: string | null;
  recurrenceGroupId: string | null;
}

export interface FarmTaskQuery {
  page?: number;
  pageSize?: number;
  sort?: string;
  search?: string;
  seasonId?: string;
  assigneeId?: string;
  status?: FarmTaskStatus;
  isOverdue?: boolean;
  from?: string;
  to?: string;
  mine?: boolean;
}

export interface FarmTaskRemindersResponse {
  asOfDate: string;
  upcomingDays: number;
  overdueCount: number;
  todayCount: number;
  upcomingCount: number;
  overdue: FarmTaskListItem[];
  today: FarmTaskListItem[];
  upcoming: FarmTaskListItem[];
}
