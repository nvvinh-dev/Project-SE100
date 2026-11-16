import type { AdminAccountQuery, AdminAccountResponse, ResetAccountPasswordRequest } from "@/features/admin/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface AdminAccountPage {
  items: AdminAccountResponse[];
  meta: PageMeta;
}

export async function getAdminAccounts(query: AdminAccountQuery): Promise<AdminAccountPage> {
  const response = await apiClient.get<ResponseEnvelope<AdminAccountResponse[]>>("/admin/accounts", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getAdminAccount(id: string): Promise<AdminAccountResponse> {
  const response = await apiClient.get<ResponseEnvelope<AdminAccountResponse>>(`/admin/accounts/${id}`);
  return response.data.data;
}

export async function lockAccount(id: string): Promise<AdminAccountResponse> {
  const response = await apiClient.post<ResponseEnvelope<AdminAccountResponse>>(`/admin/accounts/${id}/lock`);
  return response.data.data;
}

export async function unlockAccount(id: string): Promise<AdminAccountResponse> {
  const response = await apiClient.post<ResponseEnvelope<AdminAccountResponse>>(`/admin/accounts/${id}/unlock`);
  return response.data.data;
}

export async function resetAccountPassword(id: string, request: ResetAccountPasswordRequest): Promise<AdminAccountResponse> {
  const response = await apiClient.post<ResponseEnvelope<AdminAccountResponse>>(`/admin/accounts/${id}/reset-password`, request);
  return response.data.data;
}
