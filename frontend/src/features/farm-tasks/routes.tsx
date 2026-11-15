import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { FarmTaskCreatePage } from "@/features/farm-tasks/pages/FarmTaskCreatePage";
import { FarmTaskDetailPage } from "@/features/farm-tasks/pages/FarmTaskDetailPage";
import { FarmTaskEditPage } from "@/features/farm-tasks/pages/FarmTaskEditPage";
import { FarmTaskListPage } from "@/features/farm-tasks/pages/FarmTaskListPage";
import { ACCESS } from "@/lib/auth/access";

export const farmTaskRoutes: RouteObject[] = [
  { path: "/tasks", element: <FarmTaskListPage />, handle: { access: ACCESS.householdAndWorker } satisfies RouteHandle },
  {
    path: "/tasks/new",
    element: <FarmTaskCreatePage />,
    handle: { access: ACCESS.household, form: true } satisfies RouteHandle,
  },
  {
    path: "/tasks/:id",
    element: <FarmTaskDetailPage />,
    handle: { access: ACCESS.householdAndWorker } satisfies RouteHandle,
  },
  {
    path: "/tasks/:id/edit",
    element: <FarmTaskEditPage />,
    handle: { access: ACCESS.household, form: true } satisfies RouteHandle,
  },
];
