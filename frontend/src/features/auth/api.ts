import { apiClient, type ResponseEnvelope } from "@/lib/api-client";
import type { AccessTokenResponse } from "@/lib/auth/types";
import type { LoginRequest, RegisterRequest } from "@/features/auth/types";

export async function login(request: LoginRequest): Promise<AccessTokenResponse> {
  const response = await apiClient.post<ResponseEnvelope<AccessTokenResponse>>("/auth/login", request);
  return response.data.data;
}

/** Đăng ký Chủ hộ và hộ mới; chưa bắt đầu phiên, người dùng đăng nhập sau đó (UC-AUTH-01). */
export async function register(request: RegisterRequest): Promise<void> {
  await apiClient.post("/auth/register", request);
}
