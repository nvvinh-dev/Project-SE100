import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { ReportListPage } from "@/features/reports/common/pages/ReportListPage";
import { ACCESS } from "@/lib/auth/access";

/**
 * Trang báo cáo. Mỗi báo cáo khai báo đúng điều kiện quyền theo ma trận phân quyền mục 2.5 (FR-RPT-02).
 */
export const reportRoutes: RouteObject[] = [
  { path: "/reports", element: <ReportListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
];
