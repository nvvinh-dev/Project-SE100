import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { AdminAccountDetailPage } from "@/features/admin/pages/AdminAccountDetailPage";
import { AdminAccountListPage } from "@/features/admin/pages/AdminAccountListPage";
import { ACCESS } from "@/lib/auth/access";

/** Giao diện quản trị, chỉ Quản trị hệ thống; Quản trị đăng nhập xong vào thẳng `/admin`. */
export const adminRoutes: RouteObject[] = [
  { path: "/admin", element: <AdminAccountListPage />, handle: { access: ACCESS.systemAdmin } satisfies RouteHandle },
  {
    path: "/admin/accounts/:id",
    element: <AdminAccountDetailPage />,
    handle: { access: ACCESS.systemAdmin } satisfies RouteHandle,
  },
];
