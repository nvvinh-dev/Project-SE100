import type {
  CancelFarmTaskRequest,
  CancelRecurrenceGroupResponse,
  CreateFarmTaskRequest,
  CreateRecurringFarmTasksRequest,
  FarmTaskAssigneeOption,
  FarmTaskDetail,
  FarmTaskListItem,
  FarmTaskQuery,
  FarmTaskResponse,
  RecordFarmTaskResultOutcome,
  RecordFarmTaskResultRequest,
  UpdateFarmTaskRequest,
} from "@/features/farm-tasks/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

export interface FarmTaskPage {
  items: FarmTaskListItem[];
  meta: PageMeta;
}

export async function getFarmTasks(query: FarmTaskQuery): Promise<FarmTaskPage> {
  const response = await apiClient.get<ResponseEnvelope<FarmTaskListItem[]>>("/farm-tasks", { params: query });
  const meta = response.data.meta ?? {
    page: 1,
    pageSize: response.data.data.length,
    totalCount: response.data.data.length,
  };
  return { items: response.data.data, meta };
}

export async function getFarmTaskAssignees(seasonId?: string): Promise<FarmTaskAssigneeOption[]> {
  const response = await apiClient.get<ResponseEnvelope<FarmTaskAssigneeOption[]>>("/farm-tasks/assignees", {
    params: seasonId ? { seasonId } : undefined,
  });
  return response.data.data;
}

export async function getFarmTask(id: string): Promise<FarmTaskDetail> {
  const response = await apiClient.get<ResponseEnvelope<FarmTaskDetail>>(`/farm-tasks/${id}`);
  return response.data.data;
}

export async function createFarmTask(request: CreateFarmTaskRequest): Promise<FarmTaskResponse> {
  const response = await apiClient.post<ResponseEnvelope<FarmTaskResponse>>("/farm-tasks", request);
  return response.data.data;
}

export async function createRecurringFarmTasks(request: CreateRecurringFarmTasksRequest): Promise<FarmTaskResponse[]> {
  const response = await apiClient.post<ResponseEnvelope<FarmTaskResponse[]>>("/farm-tasks/recurring", request);
  return response.data.data;
}

export async function updateFarmTask(id: string, request: UpdateFarmTaskRequest): Promise<FarmTaskResponse> {
  const response = await apiClient.put<ResponseEnvelope<FarmTaskResponse>>(`/farm-tasks/${id}`, request);
  return response.data.data;
}

export async function cancelFarmTask(id: string, request: CancelFarmTaskRequest): Promise<FarmTaskResponse> {
  const response = await apiClient.post<ResponseEnvelope<FarmTaskResponse>>(`/farm-tasks/${id}/cancel`, request);
  return response.data.data;
}

export async function cancelRecurrenceGroup(recurrenceGroupId: string): Promise<CancelRecurrenceGroupResponse> {
  const response = await apiClient.post<ResponseEnvelope<CancelRecurrenceGroupResponse>>(
    `/farm-tasks/recurrence-groups/${recurrenceGroupId}/cancel`,
  );
  return response.data.data;
}

export async function recordFarmTaskResult(
  id: string,
  request: RecordFarmTaskResultRequest,
): Promise<RecordFarmTaskResultOutcome> {
  const response = await apiClient.post<ResponseEnvelope<FarmTaskDetail>>(`/farm-tasks/${id}/record-result`, request);
  return {
    data: response.data.data,
    warnings: response.data.warnings,
  };
}
