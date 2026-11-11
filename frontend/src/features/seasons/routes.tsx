import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { SeasonCreatePage } from "@/features/seasons/pages/SeasonCreatePage";
import { SeasonDetailPage } from "@/features/seasons/pages/SeasonDetailPage";
import { SeasonEditPage } from "@/features/seasons/pages/SeasonEditPage";
import { SeasonListPage } from "@/features/seasons/pages/SeasonListPage";
import { SeasonMembersPage } from "@/features/seasons/pages/SeasonMembersPage";
import { SeasonSupplyPlansPage } from "@/features/seasons/pages/SeasonSupplyPlansPage";
import { ACCESS } from "@/lib/auth/access";

// Danh sách route của module mùa vụ (FR-SEA-01, FR-SEA-04, FR-SEA-06, FR-SEA-07, FR-SEA-08, FR-SEA-11, FR-SEA-12, FR-SEA-13).
export const seasonRoutes: RouteObject[] = [
  { path: "/seasons", element: <SeasonListPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  { path: "/seasons/new", element: <SeasonCreatePage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
  { path: "/seasons/:id", element: <SeasonDetailPage />, handle: { access: ACCESS.household } satisfies RouteHandle },
  { path: "/seasons/:id/edit", element: <SeasonEditPage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
  { path: "/seasons/:id/members", element: <SeasonMembersPage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
  { path: "/seasons/:id/supply-plans", element: <SeasonSupplyPlansPage />, handle: { access: ACCESS.owner } satisfies RouteHandle },
];
