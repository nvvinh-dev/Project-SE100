import type { CurrentUserResponse } from "@/lib/auth/types";
import type { ExtraPermission, UserRole } from "@/lib/enums";

/**
 * Ai được mở một trang, thấy một mục điều hướng: các vai trò được phép, và quyền bổ sung Thành viên hộ cần có thêm.
 * Viết theo cột "Vai trò" của danh sách màn hình và ma trận phân quyền. Chỉ để tiện dùng: backend vẫn quyết định (NFR-04).
 */
export interface Access {
  roles: readonly UserRole[];
  /** Thành viên hộ cần thêm quyền bổ sung này; Chủ hộ luôn có đủ quyền. */
  permission?: ExtraPermission;
}

/** Kết quả kiểm tra: không được phép vì thiếu quyền bổ sung thì có `missingPermission` để báo người dùng xin Chủ hộ bật. */
export type AccessCheck = { allowed: true } | { allowed: false; missingPermission: ExtraPermission | null };

export type UserAccessInfo = Pick<CurrentUserResponse, "role" | "canManageStock" | "canManageFinance" | "canCloseSeason">;

/** Các nhóm quyền hay dùng, đặt tên theo ký hiệu của danh sách màn hình (CH, TV, NC, QT, +Kho, +TC). */
export const ACCESS = {
  /** Mọi người đã đăng nhập. */
  everyone: { roles: ["Owner", "Member", "Worker", "SystemAdmin"] },
  /** CH, TV. */
  household: { roles: ["Owner", "Member"] },
  /** CH, TV, NC: trang công việc. */
  householdAndWorker: { roles: ["Owner", "Member", "Worker"] },
  /** NC. */
  worker: { roles: ["Worker"] },
  /** CH. */
  owner: { roles: ["Owner"] },
  /** CH, TV+Kho. */
  stockManager: { roles: ["Owner", "Member"], permission: "ManageStock" },
  /** CH, TV+TC. */
  financeManager: { roles: ["Owner", "Member"], permission: "ManageFinance" },
  /** QT. */
  systemAdmin: { roles: ["SystemAdmin"] },
} as const satisfies Record<string, Access>;

export function checkAccess(user: UserAccessInfo, access: Access): AccessCheck {
  if (!access.roles.includes(user.role)) {
    return { allowed: false, missingPermission: null };
  }

  // Quyền bổ sung chỉ áp dụng cho Thành viên hộ (BR-USR-09); Chủ hộ luôn có.
  if (access.permission && user.role === "Member" && !hasPermission(user, access.permission)) {
    return { allowed: false, missingPermission: access.permission };
  }

  return { allowed: true };
}

export function canAccess(user: UserAccessInfo, access: Access): boolean {
  return checkAccess(user, access).allowed;
}

/** Các mục người dùng được thấy, giữ nguyên thứ tự khai báo. */
export function visibleTo<T extends { access: Access }>(items: readonly T[], user: UserAccessInfo): T[] {
  return items.filter((item) => canAccess(user, item.access));
}

function hasPermission(user: UserAccessInfo, permission: ExtraPermission): boolean {
  switch (permission) {
    case "ManageStock":
      return user.canManageStock;
    case "ManageFinance":
      return user.canManageFinance;
    case "CloseSeason":
      return user.canCloseSeason;
  }
}
