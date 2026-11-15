import type {
  CancelFarmTaskRequest,
  CancelRecurrenceGroupResponse,
  CreateFarmTaskRequest,
  FarmTaskAssigneeOption,
  FarmTaskDetail,
  FarmTaskListItem,
  FarmTaskQuery,
  FarmTaskResponse,
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

export async function createFarmTask(request: CreateFarmTaskRequest): Promise<FarmTaskResponse | FarmTaskResponse[]> {
  const response = await apiClient.post<ResponseEnvelope<FarmTaskResponse | FarmTaskResponse[]>>("/farm-tasks", request);
  return response.data.data;
}

export async function updateFarmTask(id: string, request: UpdateFarmTaskRequest): Promise<FarmTaskResponse> {
  const response = await apiClient.put<ResponseEnvelope<FarmTaskResponse>>(`/farm-tasks/${id}`, request);
  return response.data.data;
}

export async function cancelFarmTask(
  id: string,
  request: CancelFarmTaskRequest,
): Promise<FarmTaskResponse | CancelRecurrenceGroupResponse> {
  const response = await apiClient.post<ResponseEnvelope<FarmTaskResponse | CancelRecurrenceGroupResponse>>(
    `/farm-tasks/${id}/cancel`,
    request,
  );
  return response.data.data;
}
