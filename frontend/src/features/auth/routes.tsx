import type { RouteObject } from "react-router";
import { LoginPage } from "@/features/auth/pages/LoginPage";
import { RegisterPage } from "@/features/auth/pages/RegisterPage";

/** Trang cho người chưa đăng nhập; router đặt trong khung `AuthLayout`, đã đăng nhập thì chuyển đi. */
export const authRoutes: RouteObject[] = [
  { path: "/login", element: <LoginPage /> },
  { path: "/register", element: <RegisterPage /> },
];
