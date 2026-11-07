import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { StockAdjustPage } from "@/features/supplies/pages/StockAdjustPage";
import { StockInPage } from "@/features/supplies/pages/StockInPage";
import { StockTransactionListPage } from "@/features/supplies/pages/StockTransactionListPage";
import { SupplyCreatePage } from "@/features/supplies/pages/SupplyCreatePage";
import { SupplyDetailPage } from "@/features/supplies/pages/SupplyDetailPage";
import { SupplyEditPage } from "@/features/supplies/pages/SupplyEditPage";
import { SupplyListPage } from "@/features/supplies/pages/SupplyListPage";
import { ACCESS } from "@/lib/auth/access";

/** Trang vật tư. Dùng cùng điều kiện với mục "Vật tư" trong app/navigation.ts; trang form ẩn thanh tab. */
export const supplyRoutes: RouteObject[] = [
  { path: "/supplies", element: <SupplyListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/supplies/new",
    element: <SupplyCreatePage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
  { path: "/supplies/:id", element: <SupplyDetailPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/supplies/:id/edit",
    element: <SupplyEditPage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
  {
    path: "/stock/in",
    element: <StockInPage />,
    handle: { access: ACCESS.stockManager, form: true } satisfies RouteHandle,
  },
  {
    path: "/stock/adjust",
    element: <StockAdjustPage />,
    handle: { access: ACCESS.stockManager, form: true } satisfies RouteHandle,
  },
  {
    path: "/stock-transactions",
    element: <StockTransactionListPage />,
    handle: { access: ACCESS.stockManager } satisfies RouteHandle,
  },
];
