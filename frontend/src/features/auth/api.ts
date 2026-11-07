import { apiClient, type ResponseEnvelope } from "@/lib/api-client";
import type { AccessTokenResponse, CurrentUserResponse } from "@/lib/auth/types";
import type {
  ChangePasswordRequest,
  LoginRequest,
  RegisterRequest,
  UpdateProfileRequest,
} from "@/features/auth/types";

export async function login(request: LoginRequest): Promise<AccessTokenResponse> {
  const response = await apiClient.post<ResponseEnvelope<AccessTokenResponse>>("/auth/login", request);
  return response.data.data;
}

/** Đăng ký Chủ hộ và hộ mới; chưa bắt đầu phiên, người dùng đăng nhập sau đó (UC-AUTH-01). */
export async function register(request: RegisterRequest): Promise<void> {
  await apiClient.post("/auth/register", request);
}

/** Thu hồi refresh token của phiên trên thiết bị này; thiết bị khác vẫn đăng nhập (BR-USR-04). */
export async function logout(): Promise<void> {
  await apiClient.post("/auth/logout");
}

/** Đổi mật khẩu; thành công thì backend thu hồi mọi phiên của người dùng, kể cả phiên đang dùng (BR-USR-04). */
export async function changePassword(request: ChangePasswordRequest): Promise<void> {
  await apiClient.post("/auth/change-password", request);
}

export async function updateProfile(request: UpdateProfileRequest): Promise<CurrentUserResponse> {
  const response = await apiClient.put<ResponseEnvelope<CurrentUserResponse>>("/auth/me", request);
  return response.data.data;
}
