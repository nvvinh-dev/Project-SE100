import { useMutation, useQueryClient } from "@tanstack/react-query";
import { showSuccess } from "@/components/common/toast";
import { changePassword, login, logout, register, updateProfile } from "@/features/auth/api";
import { useAuth } from "@/lib/auth/auth-context";
import { currentUserKeys } from "@/lib/auth/use-current-user";

export function useLogin() {
  return useMutation({ mutationFn: login });
}

export function useRegister() {
  return useMutation({ mutationFn: register });
}

// Kết thúc phiên ở `onSuccess` của useMutation, không ở callback của `mutate`: callback của `mutate` không chạy
// nếu người dùng đã rời trang, khi đó backend đã thu hồi phiên mà giao diện vẫn như đang đăng nhập.

export function useLogout() {
  const { signOut } = useAuth();
  return useMutation({ mutationFn: logout, onSuccess: signOut });
}

/** Đổi mật khẩu xong thì backend thu hồi mọi phiên, kể cả phiên này (BR-USR-04): báo rồi về trang đăng nhập. */
export function useChangePassword() {
  const { signOut } = useAuth();
  return useMutation({
    mutationFn: changePassword,
    onSuccess: () => {
      showSuccess("Đã đổi mật khẩu. Vui lòng đăng nhập lại bằng mật khẩu mới.");
      signOut();
    },
  });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateProfile,
    // Backend trả người dùng sau khi lưu: dùng luôn làm dữ liệu `/me`, không cần gọi lại.
    onSuccess: (user) => queryClient.setQueryData(currentUserKeys.all, user),
  });
}
