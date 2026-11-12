import type {
  CancelSeasonRequest,
  CloseSeasonRequest,
  CreateSeasonAssignmentRequest,
  CreateSeasonNoteRequest,
  CreateSeasonRequest,
  CreateSeasonSupplyPlanRequest,
  CropOptionItem,
  HouseholdMemberOption,
  ReopenSeasonRequest,
  SeasonAssignmentItem,
  SeasonDetailResponse,
  SeasonListItem,
  SeasonNoteResponse,
  SeasonOptionItem,
  SeasonOptionQuery,
  SeasonQuery,
  SeasonResponse,
  SeasonSupplyPlanResponse,
  UnassignSeasonAssignmentRequest,
  UpdateSeasonNoteRequest,
  UpdateSeasonRequest,
  UpdateSeasonSupplyPlanRequest,
} from "@/features/seasons/types";
import { apiClient, type PageMeta, type ResponseEnvelope, type ResponseWarning } from "@/lib/api-client";

// Một trang của danh sách: items là data, thông tin trang ở meta.
export interface SeasonPage {
  items: SeasonListItem[];
  meta: PageMeta;
}

// Lấy danh sách mùa vụ có lọc và phân trang (FR-SEA-11).
export async function getSeasons(query: SeasonQuery): Promise<SeasonPage> {
  const response = await apiClient.get<ResponseEnvelope<SeasonListItem[]>>("/seasons", { params: query });
  const meta = response.data.meta ?? {
    page: 1,
    pageSize: response.data.data.length,
    totalCount: response.data.data.length,
  };
  return { items: response.data.data, meta };
}

// Lấy danh sách lựa chọn mùa vụ theo loại thao tác (FR-SEA-11, BR-SEA-10, BR-USR-13).
export async function getSeasonOptions(query: SeasonOptionQuery): Promise<SeasonOptionItem[]> {
  const response = await apiClient.get<ResponseEnvelope<SeasonOptionItem[]>>("/seasons/options", { params: query });
  return response.data.data;
}

// Chi tiết một mùa vụ (FR-SEA-01, FR-SEA-12).
export async function getSeason(id: string): Promise<SeasonResponse> {
  const response = await apiClient.get<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}`);
  return response.data.data;
}

// Tạo mùa vụ mới (FR-SEA-01).
export async function createSeason(request: CreateSeasonRequest): Promise<SeasonResponse> {
  const response = await apiClient.post<ResponseEnvelope<SeasonResponse>>("/seasons", request);
  return response.data.data;
}

// Sửa thông tin mùa vụ (FR-SEA-07).
export async function updateSeason(id: string, request: UpdateSeasonRequest): Promise<SeasonResponse> {
  const response = await apiClient.put<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}`, request);
  return response.data.data;
}

// Chi tiết một mùa vụ đầy đủ (FR-SEA-12).
export async function getSeasonDetail(id: string): Promise<SeasonDetailResponse> {
  const response = await apiClient.get<ResponseEnvelope<SeasonDetailResponse>>(`/seasons/${id}`);
  return response.data.data;
}

// Bắt đầu mùa vụ (FR-SEA-08).
export async function startSeason(id: string): Promise<SeasonResponse> {
  const response = await apiClient.post<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}/start`);
  return response.data.data;
}

// Chốt mùa vụ (FR-SEA-09, BR-SEA-05, BR-GEN-14).
export async function closeSeason(id: string, request?: CloseSeasonRequest): Promise<SeasonResponse> {
  const response = await apiClient.post<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}/close`, request ?? {});
  return response.data.data;
}

// Hủy mùa vụ (FR-SEA-10, BR-SEA-06, BR-GEN-06, BR-GEN-14).
export async function cancelSeason(id: string, request: CancelSeasonRequest): Promise<SeasonResponse> {
  const response = await apiClient.post<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}/cancel`, request);
  return response.data.data;
}

// Mở lại mùa vụ đã chốt (FR-SEA-14, BR-SEA-08, BR-GEN-06).
export async function reopenSeason(id: string, request: ReopenSeasonRequest): Promise<SeasonResponse> {
  const response = await apiClient.post<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}/reopen`, request);
  return response.data.data;
}

// Lấy danh sách phân công người vào mùa vụ (FR-SEA-06).
export async function getSeasonAssignments(seasonId: string): Promise<SeasonAssignmentItem[]> {
  const response = await apiClient.get<ResponseEnvelope<SeasonAssignmentItem[]>>("/season-assignments", {
    params: { seasonId },
  });
  return response.data.data;
}

// Phân công người vào mùa vụ (FR-SEA-06, BR-USR-10, BR-USR-11).
export async function assignSeason(request: CreateSeasonAssignmentRequest): Promise<SeasonAssignmentItem> {
  const response = await apiClient.post<ResponseEnvelope<SeasonAssignmentItem>>("/season-assignments", request);
  return response.data.data;
}

// Thu hồi phân công người khỏi mùa vụ (FR-SEA-06, BR-USR-12, BR-GEN-14).
export async function unassignSeason(
  id: string,
  request?: UnassignSeasonAssignmentRequest,
): Promise<{ success: boolean }> {
  const response = await apiClient.post<ResponseEnvelope<{ success: boolean }>>(`/season-assignments/${id}/unassign`, request ?? {});
  return response.data.data;
}

// Lấy danh sách ghi chú kinh nghiệm của mùa vụ (FR-SEA-13).
export async function getSeasonNotes(seasonId: string): Promise<SeasonNoteResponse[]> {
  const response = await apiClient.get<ResponseEnvelope<SeasonNoteResponse[]>>("/season-notes", {
    params: { seasonId },
  });
  return response.data.data;
}

// Thêm ghi chú kinh nghiệm cho mùa vụ (FR-SEA-13, BR-SEA-11).
export async function createSeasonNote(request: CreateSeasonNoteRequest): Promise<SeasonNoteResponse> {
  const response = await apiClient.post<ResponseEnvelope<SeasonNoteResponse>>("/season-notes", request);
  return response.data.data;
}

// Sửa ghi chú kinh nghiệm của chính mình (FR-SEA-13, BR-SEA-11).
export async function updateSeasonNote(
  id: string,
  request: UpdateSeasonNoteRequest,
): Promise<SeasonNoteResponse> {
  const response = await apiClient.put<ResponseEnvelope<SeasonNoteResponse>>(`/season-notes/${id}`, request);
  return response.data.data;
}

// Lấy danh sách thành viên hộ để phân công vào mùa vụ (FR-SEA-06).
export async function getHouseholdMembers(): Promise<HouseholdMemberOption[]> {
  const response = await apiClient.get<ResponseEnvelope<HouseholdMemberOption[]>>("/members");
  return response.data.data;
}

// Lấy danh sách cây trồng đang hoạt động để chọn khi tạo mùa vụ (FR-SEA-01, FR-SEA-02).
export async function getActiveCrops(): Promise<CropOptionItem[]> {
  const response = await apiClient.get<ResponseEnvelope<CropOptionItem[]>>("/crops", {
    params: { status: "Active", pageSize: 100 },
  });
  return response.data.data;
}

// Lấy danh sách vật tư dự kiến của mùa vụ (FR-SEA-04).
export async function getSeasonSupplyPlans(seasonId: string): Promise<SeasonSupplyPlanResponse[]> {
  const response = await apiClient.get<ResponseEnvelope<SeasonSupplyPlanResponse[]>>("/season-supply-plans", {
    params: { seasonId },
  });
  return response.data.data;
}

/** Kết quả khai báo/cập nhật vật tư dự kiến: `warnings` chứa cảnh báo thiếu tồn/dưới ngưỡng (BR-SEA-09). */
export interface SeasonSupplyPlanResult {
  plan: SeasonSupplyPlanResponse;
  warnings: ResponseWarning[];
}

// Khai báo vật tư dự kiến cho mùa vụ (FR-SEA-04, BR-SEA-09).
export async function createSeasonSupplyPlan(data: CreateSeasonSupplyPlanRequest): Promise<SeasonSupplyPlanResult> {
  const response = await apiClient.post<ResponseEnvelope<SeasonSupplyPlanResponse>>("/season-supply-plans", data);
  return { plan: response.data.data, warnings: response.data.warnings ?? [] };
}

// Cập nhật lượng vật tư dự kiến của mùa vụ (FR-SEA-04, BR-SEA-09).
export async function updateSeasonSupplyPlan(
  id: string,
  data: UpdateSeasonSupplyPlanRequest,
): Promise<SeasonSupplyPlanResult> {
  const response = await apiClient.put<ResponseEnvelope<SeasonSupplyPlanResponse>>(`/season-supply-plans/${id}`, data);
  return { plan: response.data.data, warnings: response.data.warnings ?? [] };
}

// Xóa một dòng vật tư dự kiến (FR-SEA-04).
export async function deleteSeasonSupplyPlan(id: string): Promise<void> {
  await apiClient.delete(`/season-supply-plans/${id}`);
}

