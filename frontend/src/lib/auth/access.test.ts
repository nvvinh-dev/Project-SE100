import { describe, expect, it } from "vitest";
import { ACCESS, checkAccess, visibleTo } from "@/lib/auth/access";
import type { UserRole } from "@/lib/enums";

function user(role: UserRole, permissions: { stock?: boolean; finance?: boolean } = {}) {
  return {
    role,
    canManageStock: permissions.stock ?? false,
    canManageFinance: permissions.finance ?? false,
    canCloseSeason: false,
  };
}

describe("checkAccess", () => {
  it("vai trò không có trong danh sách thì không được phép, không có quyền bổ sung nào để xin", () => {
    expect(checkAccess(user("Worker"), ACCESS.household)).toEqual({ allowed: false, missingPermission: null });
    expect(checkAccess(user("SystemAdmin"), ACCESS.household)).toEqual({ allowed: false, missingPermission: null });
  });

  it("Chủ hộ luôn có quyền bổ sung", () => {
    expect(checkAccess(user("Owner"), ACCESS.stockManager)).toEqual({ allowed: true });
    expect(checkAccess(user("Owner"), ACCESS.financeManager)).toEqual({ allowed: true });
  });

  it("Thành viên hộ thiếu quyền bổ sung thì báo đúng quyền còn thiếu", () => {
    expect(checkAccess(user("Member"), ACCESS.stockManager)).toEqual({
      allowed: false,
      missingPermission: "ManageStock",
    });
    expect(checkAccess(user("Member", { stock: true }), ACCESS.financeManager)).toEqual({
      allowed: false,
      missingPermission: "ManageFinance",
    });
  });

  it("Thành viên hộ có quyền bổ sung thì được phép", () => {
    expect(checkAccess(user("Member", { stock: true }), ACCESS.stockManager)).toEqual({ allowed: true });
    expect(checkAccess(user("Member", { finance: true }), ACCESS.financeManager)).toEqual({ allowed: true });
  });

  it("Nhân công không được trang cần quyền bổ sung, dù chỉ thiếu vai trò", () => {
    expect(checkAccess(user("Worker"), ACCESS.stockManager)).toEqual({ allowed: false, missingPermission: null });
  });
});

describe("visibleTo", () => {
  it("chỉ giữ mục người dùng được thấy, đúng thứ tự khai báo", () => {
    const items = [
      { name: "Vật tư", access: ACCESS.household },
      { name: "Lịch sử kho", access: ACCESS.stockManager },
      { name: "Thành viên", access: ACCESS.owner },
      { name: "Tài khoản", access: ACCESS.everyone },
    ];

    expect(visibleTo(items, user("Member")).map((i) => i.name)).toEqual(["Vật tư", "Tài khoản"]);
    expect(visibleTo(items, user("Member", { stock: true })).map((i) => i.name)).toEqual([
      "Vật tư",
      "Lịch sử kho",
      "Tài khoản",
    ]);
    expect(visibleTo(items, user("Owner")).map((i) => i.name)).toEqual([
      "Vật tư",
      "Lịch sử kho",
      "Thành viên",
      "Tài khoản",
    ]);
  });
});
