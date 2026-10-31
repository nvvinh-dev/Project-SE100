import { createBrowserRouter } from "react-router";
import { AuthLayout } from "@/app/layouts/AuthLayout";
import { authRoutes } from "@/features/auth/routes";
import { RedirectIfAuthenticated } from "@/lib/auth/RedirectIfAuthenticated";
import { RequireAuth } from "@/lib/auth/RequireAuth";

// Route của các module (features/<module>/routes.tsx) ghép vào đây.
// Không dùng loader, action: dữ liệu trang lấy qua TanStack Query.
export const router = createBrowserRouter([
  {
    element: <RedirectIfAuthenticated />,
    children: [{ element: <AuthLayout />, children: authRoutes }],
  },
  {
    element: <RequireAuth />,
    children: [
      {
        path: "/",
        // Trang tạm cho tới khi có bố cục và trang "Hôm nay".
        element: (
          <main className="flex min-h-svh items-center justify-center p-4">
            <h1 className="text-2xl font-semibold text-primary">Sổ Tay Nhà Nông</h1>
          </main>
        ),
      },
    ],
  },
]);
