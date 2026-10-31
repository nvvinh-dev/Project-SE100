import { useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useCallback, useEffect, useMemo, useState } from "react";
import { showError } from "@/components/common/toast";
import { refreshAccessToken, setSessionEndedHandler } from "@/lib/api-client";
import type { ApiError } from "@/lib/api-error";
import { AuthContext, type AuthStatus } from "@/lib/auth/auth-context";
import { setAccessToken } from "@/lib/auth/token-store";

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>("loading");

  useEffect(() => {
    let active = true;
    // Mở ứng dụng, tải lại trang: access token trong bộ nhớ đã mất, lấy token mới bằng cookie refresh token (FR-AUTH-03).
    void refreshAccessToken().then((refreshed) => {
      if (active) {
        setStatus(refreshed ? "authenticated" : "anonymous");
      }
    });
    return () => {
      active = false;
    };
  }, []);

  const signIn = useCallback(
    (accessToken: string) => {
      setAccessToken(accessToken);
      // Dữ liệu đã tải là của phiên trước, có thể của tài khoản khác trên cùng điện thoại.
      // Chỉ bỏ query, không bỏ mutation: lời gọi đăng nhập đang chạy là một mutation.
      queryClient.removeQueries();
      setStatus("authenticated");
    },
    [queryClient],
  );

  const endSession = useCallback(
    (error?: ApiError) => {
      setAccessToken(null);
      queryClient.clear();
      setStatus("anonymous");
      if (error) {
        showError(error);
      }
    },
    [queryClient],
  );

  useEffect(() => {
    setSessionEndedHandler(endSession);
    return () => setSessionEndedHandler(null);
  }, [endSession]);

  const value = useMemo(() => ({ status, signIn }), [status, signIn]);

  return <AuthContext value={value}>{children}</AuthContext>;
}
