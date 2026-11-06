import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { PlotCreatePage } from "@/features/plots/pages/PlotCreatePage";
import { PlotDetailPage } from "@/features/plots/pages/PlotDetailPage";
import { PlotEditPage } from "@/features/plots/pages/PlotEditPage";
import { PlotListPage } from "@/features/plots/pages/PlotListPage";
import { ACCESS } from "@/lib/auth/access";

/**
 * Trang thửa đất. Mỗi route khai báo ai được mở (cột "Vai trò" của danh sách màn hình), dùng cùng điều kiện với mục
 * "Thửa đất" trong app/navigation.ts; trang form khai báo `form: true` để ẩn thanh tab.
 */
export const plotRoutes: RouteObject[] = [
  { path: "/plots", element: <PlotListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/plots/new",
    element: <PlotCreatePage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
  { path: "/plots/:id", element: <PlotDetailPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/plots/:id/edit",
    element: <PlotEditPage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
];
