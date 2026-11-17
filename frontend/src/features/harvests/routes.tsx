import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { HarvestCorrectPage } from "@/features/harvests/pages/HarvestCorrectPage";
import { HarvestCreatePage } from "@/features/harvests/pages/HarvestCreatePage";
import { HarvestDetailPage } from "@/features/harvests/pages/HarvestDetailPage";
import { HarvestListPage } from "@/features/harvests/pages/HarvestListPage";
import { ProduceStockAdjustPage } from "@/features/harvests/pages/ProduceStockAdjustPage";
import { ProduceStockPage } from "@/features/harvests/pages/ProduceStockPage";
import { ACCESS } from "@/lib/auth/access";

/** Trang phiếu thu hoạch và tồn nông sản (Chủ hộ, Thành viên hộ; điều chỉnh tồn chỉ Chủ hộ). */
export const harvestRoutes: RouteObject[] = [
  { path: "/harvests", element: <HarvestListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/harvests/new",
    element: <HarvestCreatePage />,
    handle: { access: ACCESS.household, form: true } satisfies RouteHandle,
  },
  {
    path: "/harvests/:id",
    element: <HarvestDetailPage />,
    handle: { access: ACCESS.household } satisfies RouteHandle,
  },
  {
    path: "/harvests/:id/correct",
    element: <HarvestCorrectPage />,
    handle: { access: ACCESS.household, form: true } satisfies RouteHandle,
  },
  {
    path: "/produce-stock",
    element: <ProduceStockPage />,
    handle: { access: ACCESS.household } satisfies RouteHandle,
  },
  {
    path: "/produce-stock/adjust",
    element: <ProduceStockAdjustPage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
];
