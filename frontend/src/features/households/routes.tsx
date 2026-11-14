import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { HouseholdPage } from "@/features/households/pages/HouseholdPage";
import { HouseholdSettingsPage } from "@/features/households/pages/HouseholdSettingsPage";
import { MemberCreatePage } from "@/features/households/pages/MemberCreatePage";
import { MemberDetailPage } from "@/features/households/pages/MemberDetailPage";
import { MemberListPage } from "@/features/households/pages/MemberListPage";
import { ACCESS } from "@/lib/auth/access";

/**
 * Trang hộ, thành viên, cài đặt hộ. Quyền mở trang dùng cùng điều kiện với nhóm "Quản lý hộ" trong app/navigation.ts;
 * trang form khai báo `form: true` để ẩn thanh tab.
 */
export const householdRoutes: RouteObject[] = [
  { path: "/household", element: <HouseholdPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/household/settings",
    element: <HouseholdSettingsPage />,
    handle: { access: ACCESS.owner } satisfies RouteHandle,
  },
  { path: "/members", element: <MemberListPage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
  {
    path: "/members/new",
    element: <MemberCreatePage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
  { path: "/members/:id", element: <MemberDetailPage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
];
