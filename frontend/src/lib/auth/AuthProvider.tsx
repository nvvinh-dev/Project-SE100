import { useQueryClient } from "@tanstack/react-query";
import { type ReactNode, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showError } from "@/components/common/toast";
import { refreshAccessToken, setSessionEndedHandler } from "@/lib/api-client";
import type { ApiError } from "@/lib/api-error";
import { AuthContext, type AuthStatus } from "@/lib/auth/auth-context";
import { setAccessToken } from "@/lib/auth/token-store";

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [status, setStatus] = useState<AuthStatus>("loading");
  // Lỗi tạm thời (mất mạng, máy chủ lỗi) khi khôi phục phiên lúc mở ứng dụng.
  const [restoreError, setRestoreError] = useState<unknown>(null);
  // Nhiều yêu cầu đang chạy cùng nhận ACCOUNT_LOCKED hay cùng hết phiên: chỉ xử lý lần đầu, không hiện nhiều thông báo.
  const sessionEndedRef = useRef(false);

  // Mở ứng dụng, tải lại trang: access token trong bộ nhớ đã mất, lấy token mới bằng cookie refresh token (FR-AUTH-03).
  const restoreSession = useCallback(() => {
    void refreshAccessToken().then(
      (refreshed) => setStatus(refreshed ? "authenticated" : "anonymous"),
      // Refresh token có thể vẫn còn hạn: báo lỗi và cho thử lại, không đưa về trang đăng nhập.
      (error: unknown) => setRestoreError(error),
    );
  }, []);

  useEffect(() => {
    restoreSession();
  }, [restoreSession]);

  const retryRestore = useCallback(() => {
    setRestoreError(null);
    restoreSession();
  }, [restoreSession]);

  const signIn = useCallback(
    (accessToken: string) => {
      setAccessToken(accessToken);
      sessionEndedRef.current = false;
      // Dữ liệu đã tải là của phiên trước, có thể của tài khoản khác trên cùng điện thoại.
      // Chỉ bỏ query, không bỏ mutation: lời gọi đăng nhập đang chạy là một mutation.
      queryClient.removeQueries();
      setStatus("authenticated");
    },
    [queryClient],
  );

  const endSession = useCallback(
    (error?: ApiError) => {
      if (sessionEndedRef.current) {
        return;
      }
      sessionEndedRef.current = true;

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

  if (restoreError !== null) {
    return (
      <main className="mx-auto flex min-h-svh w-full max-w-sm items-center p-4">
        <div className="w-full">
          <ErrorAlert error={restoreError} onRetry={retryRestore} />
        </div>
      </main>
    );
  }

  return <AuthContext value={value}>{children}</AuthContext>;
}
