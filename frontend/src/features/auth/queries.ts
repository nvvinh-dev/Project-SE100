import { useMutation, useQueryClient } from "@tanstack/react-query";
import { changePassword, login, logout, register, updateProfile } from "@/features/auth/api";
import { currentUserKeys } from "@/lib/auth/use-current-user";

export function useLogin() {
  return useMutation({ mutationFn: login });
}

export function useRegister() {
  return useMutation({ mutationFn: register });
}

export function useLogout() {
  return useMutation({ mutationFn: logout });
}

export function useChangePassword() {
  return useMutation({ mutationFn: changePassword });
}

export function useUpdateProfile() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateProfile,
    // Backend trả người dùng sau khi lưu: dùng luôn làm dữ liệu `/me`, không cần gọi lại.
    onSuccess: (user) => queryClient.setQueryData(currentUserKeys.all, user),
  });
}
