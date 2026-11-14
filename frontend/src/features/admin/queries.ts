import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { getAdminAccount, getAdminAccounts, lockAccount, resetAccountPassword, unlockAccount } from "@/features/admin/api";
import type { AdminAccountQuery, ResetAccountPasswordRequest } from "@/features/admin/types";

export const adminKeys = {
  all: ["admin"] as const,
  list: (query: AdminAccountQuery) => [...adminKeys.all, "accounts", "list", query] as const,
  detail: (id: string) => [...adminKeys.all, "accounts", "detail", id] as const,
};

export function useAdminAccounts(query: AdminAccountQuery) {
  return useQuery({
    queryKey: adminKeys.list(query),
    queryFn: () => getAdminAccounts(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}

export function useAdminAccount(id: string) {
  return useQuery({ queryKey: adminKeys.detail(id), queryFn: () => getAdminAccount(id) });
}

export function useLockAccount(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => lockAccount(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.all }),
  });
}

export function useUnlockAccount(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => unlockAccount(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: adminKeys.all }),
  });
}

export function useResetAccountPassword(id: string) {
  return useMutation({ mutationFn: (request: ResetAccountPasswordRequest) => resetAccountPassword(id, request) });
}
