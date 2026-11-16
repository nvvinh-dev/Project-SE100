import type { PageQuery } from "@/lib/api-client";
import type { UserRole } from "@/lib/enums";

// Trùng tên DTO backend (Features/Admin/AdminDtos.cs).

/** Một tài khoản thuộc hộ, ở mức thông tin tài khoản; không có tài khoản Quản trị (BR-USR-06). */
export interface AdminAccountResponse {
  id: string;
  fullName: string;
  phone: string;
  role: UserRole;
  householdCode: string;
  householdName: string;
  isLocked: boolean;
  createdAt: string;
}

/** Tham số danh sách; `search` tìm họ tên, số điện thoại, mã hộ, tên hộ; `sort`: `fullName`, `householdCode`, `createdAt`. */
export interface AdminAccountQuery extends PageQuery {
  role?: UserRole;
  isLocked?: boolean;
}

export interface ResetAccountPasswordRequest {
  newPassword: string;
}
