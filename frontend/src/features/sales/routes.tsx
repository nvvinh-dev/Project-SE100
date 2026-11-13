import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { CustomerCreatePage } from "@/features/sales/pages/CustomerCreatePage";
import { CustomerEditPage } from "@/features/sales/pages/CustomerEditPage";
import { CustomerListPage } from "@/features/sales/pages/CustomerListPage";
import { ACCESS } from "@/lib/auth/access";

/**
 * Trang bán hàng, khách hàng (Chủ hộ, TV+TC). Dùng cùng điều kiện với các mục nhóm "Tài chính" trong app/navigation.ts;
 * trang form ẩn thanh tab.
 */
export const salesRoutes: RouteObject[] = [
  { path: "/customers", element: <CustomerListPage />, handle: { access: ACCESS.financeManager } satisfies RouteHandle },
  {
    path: "/customers/new",
    element: <CustomerCreatePage />,
    handle: { access: ACCESS.financeManager, form: true } satisfies RouteHandle,
  },
  {
    path: "/customers/:id/edit",
    element: <CustomerEditPage />,
    handle: { access: ACCESS.financeManager, form: true } satisfies RouteHandle,
  },
];
