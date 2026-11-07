import { createContext, use } from "react";

/**
 * `loading`: đang làm mới phiên khi mở ứng dụng; `authenticated`: đã có access token;
 * `anonymous`: chưa đăng nhập, hoặc phiên đã kết thúc.
 */
export type AuthStatus = "loading" | "authenticated" | "anonymous";

export interface AuthContextValue {
  status: AuthStatus;
  /**
   * Phiên vừa kết thúc do chính người dùng (đăng xuất, đổi mật khẩu). Khi đó trang đăng nhập không nhớ trang đang mở,
   * vì một điện thoại có thể dùng chung trong hộ.
   */
  signedOut: boolean;
  /** Bắt đầu phiên bằng access token vừa nhận khi đăng nhập. */
  signIn: (accessToken: string) => void;
  /** Kết thúc phiên sau khi backend đã thu hồi refresh token: xóa token, dữ liệu đã tải, về trang đăng nhập. */
  signOut: () => void;
}

export const AuthContext = createContext<AuthContextValue | null>(null);

export function useAuth(): AuthContextValue {
  const value = use(AuthContext);
  if (!value) {
    throw new Error("useAuth phải dùng bên trong AuthProvider.");
  }
  return value;
}
