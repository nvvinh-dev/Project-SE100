import type { UserRole } from "@/lib/enums";

/** Body của đăng nhập, làm mới phiên: chỉ có access token, refresh token nằm trong cookie HttpOnly (D-12). */
export interface AccessTokenResponse {
  accessToken: string;
}

/** Người đang đăng nhập (`GET /api/auth/me`). */
export interface CurrentUserResponse {
  id: string;
  fullName: string;
  phone: string;
  role: UserRole;
  /** Để trống với Quản trị hệ thống. */
  household: CurrentUserHousehold | null;
  /** Ba quyền bổ sung chỉ có ý nghĩa với Thành viên hộ; Chủ hộ luôn có đủ quyền. */
  canManageStock: boolean;
  canManageFinance: boolean;
  canCloseSeason: boolean;
}

export interface CurrentUserHousehold {
  code: string;
  name: string;
}
