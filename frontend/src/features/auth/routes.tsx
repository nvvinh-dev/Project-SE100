import type { RouteObject } from "react-router";
import type { RouteHandle } from "@/app/route-handle";
import { AccountPage } from "@/features/auth/pages/AccountPage";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { RegisterPage } from "@/features/auth/pages/RegisterPage";
import { ACCESS } from "@/lib/auth/access";

/** Trang cho người chưa đăng nhập; router đặt trong khung `AuthLayout`, đã đăng nhập thì chuyển đi. */
export const authRoutes: RouteObject[] = [
  { path: "/login", element: <LoginPage /> },
  { path: "/register", element: <RegisterPage /> },
];

/** Trang của người đã đăng nhập, router đặt trong khung `AppLayout`. */
export const accountRoutes: RouteObject[] = [
  { path: "/account", element: <AccountPage />, handle: { access: ACCESS.everyone } satisfies RouteHandle },
];
