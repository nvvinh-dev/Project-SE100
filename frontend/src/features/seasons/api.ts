import type {
  CreateSeasonRequest,
  SeasonListItem,
  SeasonOptionItem,
  SeasonOptionQuery,
  SeasonQuery,
  SeasonResponse,
  UpdateSeasonRequest,
} from "@/features/seasons/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

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

// Sửa mùa vụ (FR-SEA-07).
export async function updateSeason(id: string, request: UpdateSeasonRequest): Promise<SeasonResponse> {
  const response = await apiClient.put<ResponseEnvelope<SeasonResponse>>(`/seasons/${id}`, request);
  return response.data.data;
}
