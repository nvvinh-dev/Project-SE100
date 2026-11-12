import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { ExpenseCorrectPage } from "@/features/expenses/pages/ExpenseCorrectPage";
import { ExpenseCreatePage } from "@/features/expenses/pages/ExpenseCreatePage";
import { ExpenseDetailPage } from "@/features/expenses/pages/ExpenseDetailPage";
import { ExpenseListPage } from "@/features/expenses/pages/ExpenseListPage";
import { ACCESS } from "@/lib/auth/access";

/** Trang chi phí (Chủ hộ, TV+TC). Dùng cùng điều kiện với mục "Chi phí" trong app/navigation.ts; trang form ẩn thanh tab. */
export const expenseRoutes: RouteObject[] = [
  { path: "/expenses", element: <ExpenseListPage />, handle: { access: ACCESS.financeManager } satisfies RouteHandle },
  {
    path: "/expenses/new",
    element: <ExpenseCreatePage />,
    handle: { access: ACCESS.financeManager, form: true } satisfies RouteHandle,
  },
  {
    path: "/expenses/:id",
    element: <ExpenseDetailPage />,
    handle: { access: ACCESS.financeManager } satisfies RouteHandle,
  },
  {
    path: "/expenses/:id/correct",
    element: <ExpenseCorrectPage />,
    handle: { access: ACCESS.financeManager, form: true } satisfies RouteHandle,
  },
];
