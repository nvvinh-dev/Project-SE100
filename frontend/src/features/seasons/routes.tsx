import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { SeasonListPage } from "@/features/seasons/pages/SeasonListPage";
import { ACCESS } from "@/lib/auth/access";

// Danh sách route của module mùa vụ (FR-SEA-11).
export const seasonRoutes: RouteObject[] = [
  { path: "/seasons", element: <SeasonListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
];
