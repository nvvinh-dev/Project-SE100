import type {
  ChangeHouseholdSettingsRequest,
  ChangeMemberPermissionsRequest,
  ChangeMemberRoleRequest,
  CreateMemberRequest,
  HouseholdResponse,
  HouseholdSettingsResponse,
  MemberListItem,
  MemberQuery,
  MemberResponse,
  ResetMemberPasswordRequest,
  UpdateHouseholdRequest,
  UpdateMemberRequest,
} from "@/features/households/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

export async function getHousehold(): Promise<HouseholdResponse> {
  const response = await apiClient.get<ResponseEnvelope<HouseholdResponse>>("/household");
  return response.data.data;
}

export async function updateHousehold(request: UpdateHouseholdRequest): Promise<HouseholdResponse> {
  const response = await apiClient.put<ResponseEnvelope<HouseholdResponse>>("/household", request);
  return response.data.data;
}

export async function getHouseholdSettings(): Promise<HouseholdSettingsResponse> {
  const response = await apiClient.get<ResponseEnvelope<HouseholdSettingsResponse>>("/household/settings");
  return response.data.data;
}

export async function changeHouseholdSettings(request: ChangeHouseholdSettingsRequest): Promise<HouseholdSettingsResponse> {
  const response = await apiClient.post<ResponseEnvelope<HouseholdSettingsResponse>>("/household/change-settings", request);
  return response.data.data;
}

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface MemberPage {
  items: MemberListItem[];
  meta: PageMeta;
}

export async function getMembers(query: MemberQuery): Promise<MemberPage> {
  const response = await apiClient.get<ResponseEnvelope<MemberListItem[]>>("/members", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getMember(id: string): Promise<MemberResponse> {
  const response = await apiClient.get<ResponseEnvelope<MemberResponse>>(`/members/${id}`);
  return response.data.data;
}

export async function createMember(request: CreateMemberRequest): Promise<MemberResponse> {
  const response = await apiClient.post<ResponseEnvelope<MemberResponse>>("/members", request);
  return response.data.data;
}

export async function updateMember(id: string, request: UpdateMemberRequest): Promise<MemberResponse> {
  const response = await apiClient.put<ResponseEnvelope<MemberResponse>>(`/members/${id}`, request);
  return response.data.data;
}

export async function changeMemberRole(id: string, request: ChangeMemberRoleRequest): Promise<MemberResponse> {
  const response = await apiClient.post<ResponseEnvelope<MemberResponse>>(`/members/${id}/change-role`, request);
  return response.data.data;
}

export async function changeMemberPermissions(id: string, request: ChangeMemberPermissionsRequest): Promise<MemberResponse> {
  const response = await apiClient.post<ResponseEnvelope<MemberResponse>>(`/members/${id}/change-permissions`, request);
  return response.data.data;
}

export async function lockMember(id: string): Promise<MemberResponse> {
  const response = await apiClient.post<ResponseEnvelope<MemberResponse>>(`/members/${id}/lock`);
  return response.data.data;
}

export async function unlockMember(id: string): Promise<MemberResponse> {
  const response = await apiClient.post<ResponseEnvelope<MemberResponse>>(`/members/${id}/unlock`);
  return response.data.data;
}

export async function resetMemberPassword(id: string, request: ResetMemberPasswordRequest): Promise<MemberResponse> {
  const response = await apiClient.post<ResponseEnvelope<MemberResponse>>(`/members/${id}/reset-password`, request);
  return response.data.data;
}
