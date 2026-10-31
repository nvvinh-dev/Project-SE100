import { Navigate, Outlet, useLocation } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { useAuth } from "@/lib/auth/auth-context";
import { homePathFor } from "@/lib/auth/home-path";
import { readLoginLocationState } from "@/lib/auth/login-location-state";
import { useCurrentUser } from "@/lib/auth/use-current-user";

/**
 * Bọc `/login`, `/register`. Đã đăng nhập thì chuyển đi: về trang đang mở trước khi bị đưa tới đăng nhập,
 * nếu không có thì về trang đầu theo vai trò. Đăng nhập xong cũng đi đường này.
 */
export function RedirectIfAuthenticated() {
  const { status } = useAuth();
  const location = useLocation();
  const currentUser = useCurrentUser();

  if (status === "anonymous") {
    return <Outlet />;
  }

  if (currentUser.data) {
    const { from } = readLoginLocationState(location.state);
    return <Navigate to={from ?? homePathFor(currentUser.data.role)} replace />;
  }

  if (currentUser.isError) {
    return (
      <div className="mx-auto w-full max-w-sm p-4">
        <ErrorAlert error={currentUser.error} onRetry={() => void currentUser.refetch()} />
      </div>
    );
  }

  return (
    <div className="mx-auto w-full max-w-sm p-4">
      <LoadingState variant="form" />
    </div>
  );
}
