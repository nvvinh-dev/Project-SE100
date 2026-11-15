import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { AuditLogListPage } from "@/features/audit-logs/pages/AuditLogListPage";
import { ACCESS } from "@/lib/auth/access";

/** Nhật ký thao tác, chỉ Chủ hộ; cùng điều kiện với mục "Nhật ký thao tác" trong app/navigation.ts. */
export const auditLogRoutes: RouteObject[] = [
  { path: "/audit-logs", element: <AuditLogListPage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
];
