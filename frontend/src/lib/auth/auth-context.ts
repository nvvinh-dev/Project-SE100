import { createContext, use } from "react";

/**
 * `loading`: đang làm mới phiên khi mở ứng dụng; `authenticated`: đã có access token;
 * `anonymous`: chưa đăng nhập, hoặc phiên đã kết thúc.
 */
export type AuthStatus = "loading" | "authenticated" | "anonymous";

export interface AuthContextValue {
  status: AuthStatus;
  /** Bắt đầu phiên bằng access token vừa nhận khi đăng nhập. */
  signIn: (accessToken: string) => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = use(AuthContext);
  if (!value) {
    throw new Error("useAuth phải dùng bên trong AuthProvider.");
  }
  return value;
}
