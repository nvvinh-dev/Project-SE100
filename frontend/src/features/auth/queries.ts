import { useMutation } from "@tanstack/react-query";
import { login, register } from "@/features/auth/api";

export function useLogin() {
  return useMutation({ mutationFn: login });
}

export function useRegister() {
  return useMutation({ mutationFn: register });
}
