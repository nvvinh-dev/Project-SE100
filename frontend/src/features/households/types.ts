import type { PageQuery } from "@/lib/api-client";
import type { UserRole } from "@/lib/enums";

// Trùng tên DTO backend (Features/Households/HouseholdDtos.cs, MemberDtos.cs).

/** Thông tin hộ của người đang đăng nhập. Mã hộ do hệ thống sinh, không sửa được (BR-CAT-02). */
export interface HouseholdResponse {
  code: string;
  name: string;
  productionAddress: string;
  contactInfo: string | null;
  /** Gửi kèm khi sửa để không ghi đè thay đổi của người khác (BR-GEN-11). */
  version: number;
}

export interface UpdateHouseholdRequest {
  name: string;
  productionAddress: string;
  contactInfo: string | null;
  version: number;
}

/** Hai công tắc cài đặt hộ; dùng chung `version` với thông tin hộ vì cùng một dòng dữ liệu. */
export interface HouseholdSettingsResponse {
  allowNegativeStock: boolean;
  allowOverlappingSeasons: boolean;
  version: number;
}

export interface ChangeHouseholdSettingsRequest {
  allowNegativeStock: boolean;
  allowOverlappingSeasons: boolean;
  version: number;
}

/** Chi tiết một người trong hộ, chỉ Chủ hộ xem. */
export interface MemberResponse {
  id: string;
  fullName: string;
  phone: string;
  role: UserRole;
  canManageStock: boolean;
  canManageFinance: boolean;
  canCloseSeason: boolean;
  isLocked: boolean;
  version: number;
}

/** Một dòng danh sách. Thành viên hộ chỉ nhận họ tên, vai trò; trang danh sách chỉ dành cho Chủ hộ nên luôn đủ. */
export interface MemberListItem {
  id: string;
  fullName: string;
  phone: string | null;
  role: UserRole;
  canManageStock: boolean | null;
  canManageFinance: boolean | null;
  canCloseSeason: boolean | null;
  isLocked: boolean | null;
}

/** Tham số danh sách; `sort`: `fullName` (mặc định), `createdAt`, thêm `-` để giảm dần. */
export interface MemberQuery extends PageQuery {
  role?: UserRole;
  isLocked?: boolean;
}

/** Hai vai trò Chủ hộ tạo, đổi qua lại được (BR-USR-08). */
export const memberRoles = ["Member", "Worker"] as const satisfies readonly UserRole[];
export type MemberRole = (typeof memberRoles)[number];

/** Ba quyền bổ sung mặc định tắt. */
export interface CreateMemberRequest {
  fullName: string;
  phone: string;
  role: MemberRole;
  initialPassword: string;
}

export interface UpdateMemberRequest {
  fullName: string;
  phone: string;
  version: number;
}

export interface ChangeMemberRoleRequest {
  role: MemberRole;
  version: number;
}

export interface ChangeMemberPermissionsRequest {
  canManageStock: boolean;
  canManageFinance: boolean;
  canCloseSeason: boolean;
  version: number;
}

export interface ResetMemberPasswordRequest {
  newPassword: string;
}
