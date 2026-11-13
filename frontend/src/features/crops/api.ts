import type {
  CreateCropRequest,
  CropListItem,
  CropQuery,
  CropResponse,
  UpdateCropRequest,
} from "@/features/crops/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface CropPage {
  items: CropListItem[];
  meta: PageMeta;
}

export async function getCrops(query: CropQuery): Promise<CropPage> {
  const response = await apiClient.get<ResponseEnvelope<CropListItem[]>>("/crops", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getCrop(id: string): Promise<CropResponse> {
  const response = await apiClient.get<ResponseEnvelope<CropResponse>>(`/crops/${id}`);
  return response.data.data;
}

export async function createCrop(request: CreateCropRequest): Promise<CropResponse> {
  const response = await apiClient.post<ResponseEnvelope<CropResponse>>("/crops", request);
  return response.data.data;
}

export async function updateCrop(id: string, request: UpdateCropRequest): Promise<CropResponse> {
  const response = await apiClient.put<ResponseEnvelope<CropResponse>>(`/crops/${id}`, request);
  return response.data.data;
}
