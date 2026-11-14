import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { CropCreatePage } from "@/features/crops/pages/CropCreatePage";
import { CropEditPage } from "@/features/crops/pages/CropEditPage";
import { CropListPage } from "@/features/crops/pages/CropListPage";
import { ACCESS } from "@/lib/auth/access";

/**
 * Trang cây trồng. Không có trang chi tiết cây: Chủ hộ chạm một dòng để mở trang sửa, Thành viên hộ chỉ xem danh sách.
 * Quyền mở trang dùng cùng điều kiện với mục "Cây trồng" trong app/navigation.ts.
 */
export const cropRoutes: RouteObject[] = [
  { path: "/crops", element: <CropListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  {
    path: "/crops/new",
    element: <CropCreatePage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
  {
    path: "/crops/:id/edit",
    element: <CropEditPage />,
    handle: { access: ACCESS.owner, form: true } satisfies RouteHandle,
  },
];
