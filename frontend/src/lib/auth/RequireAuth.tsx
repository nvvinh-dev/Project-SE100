import { Navigate, Outlet, useLocation } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { useAuth } from "@/lib/auth/auth-context";
import type { LoginLocationState } from "@/lib/auth/login-location-state";
import { useCurrentUser } from "@/lib/auth/use-current-user";

/**
 * Bọc các trang cần đăng nhập. Chưa đăng nhập, hoặc làm mới phiên thất bại, thì chuyển tới `/login` và nhớ trang
 * đang mở để quay lại sau khi đăng nhập. Trang chỉ hiện khi đã có người dùng hiện tại.
 */
export function RequireAuth() {
  const { status } = useAuth();
  const location = useLocation();
  const currentUser = useCurrentUser();

  if (status === "anonymous") {
    const state: LoginLocationState = { from: location.pathname + location.search + location.hash };
    return <Navigate to="/login" replace state={state} />;
  }

  // Đã có dữ liệu thì hiện trang dù lần tải lại sau đó lỗi.
  if (currentUser.data) {
    return <Outlet />;
  }

  if (currentUser.isError) {
    return (
      <div className="mx-auto w-full max-w-md p-4">
        <ErrorAlert error={currentUser.error} onRetry={() => void currentUser.refetch()} />
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-5xl p-4">
      <LoadingState variant="detail" />
    </div>
  );
}
