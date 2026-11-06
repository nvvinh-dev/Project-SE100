import { createBrowserRouter, matchRoutes, type RouteObject } from "react-router";
import { AppLayout } from "@/app/layouts/AppLayout";
import { AuthLayout } from "@/app/layouts/AuthLayout";
import { MorePage } from "@/app/pages/MorePage";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { TodayPage } from "@/app/pages/TodayPage";
import { mergeRouteHandles, type RouteHandle } from "@/app/route-handle";
import { accountRoutes, authRoutes } from "@/features/auth/routes";
import { plotRoutes } from "@/features/plots/routes";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { RedirectIfAuthenticated } from "@/lib/auth/RedirectIfAuthenticated";
import { RequireAuth } from "@/lib/auth/RequireAuth";
import type { CurrentUserResponse } from "@/lib/auth/types";

// Route của các module (features/<module>/routes.tsx) ghép vào phần con của AppLayout. Mỗi route khai báo ai được mở
// bằng `handle: { access }` (xem RouteHandle). Không dùng loader, action: dữ liệu trang lấy qua TanStack Query.
const routes: RouteObject[] = [
  {
    element: <RedirectIfAuthenticated canOpen={canOpenPath} />,
    children: [{ element: <AuthLayout />, children: authRoutes }],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <AppLayout />,
        children: [
          // Nhân công, Quản trị mở `/` thì TodayPage chuyển tới trang đầu của họ, nên route này không khai báo quyền.
          { path: "/", element: <TodayPage /> },
          { path: "/more", element: <MorePage />, handle: { access: ACCESS.household } satisfies RouteHandle },
          ...accountRoutes,
          ...plotRoutes,
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
];

export const router = createBrowserRouter(routes);

/**
 * Đường dẫn đã nhớ trước khi đăng nhập có mở được với vai trò vừa đăng nhập không. Dùng đúng điều kiện quyền khai báo
 * cạnh route, để không quay lại một trang chỉ hiện "không có quyền".
 */
function canOpenPath(path: string, user: CurrentUserResponse): boolean {
  const { pathname } = new URL(path, window.location.origin);
  const matches = matchRoutes(routes, pathname) ?? [];
  const { access } = mergeRouteHandles(matches.map((match) => ({ handle: match.route.handle })));
  return access === undefined || canAccess(user, access);
}
