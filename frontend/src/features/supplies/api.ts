import type {
  CreateSupplyRequest,
  SupplyListItem,
  SupplyQuery,
  SupplyResponse,
  UpdateSupplyRequest,
} from "@/features/supplies/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface SupplyPage {
  items: SupplyListItem[];
  meta: PageMeta;
}

export async function getSupplies(query: SupplyQuery): Promise<SupplyPage> {
  const response = await apiClient.get<ResponseEnvelope<SupplyListItem[]>>("/supplies", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getSupply(id: string): Promise<SupplyResponse> {
  const response = await apiClient.get<ResponseEnvelope<SupplyResponse>>(`/supplies/${id}`);
  return response.data.data;
}

export async function createSupply(request: CreateSupplyRequest): Promise<SupplyResponse> {
  const response = await apiClient.post<ResponseEnvelope<SupplyResponse>>("/supplies", request);
  return response.data.data;
}

export async function updateSupply(id: string, request: UpdateSupplyRequest): Promise<SupplyResponse> {
  const response = await apiClient.put<ResponseEnvelope<SupplyResponse>>(`/supplies/${id}`, request);
  return response.data.data;
}
