import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  changeHouseholdSettings,
  changeMemberPermissions,
  changeMemberRole,
  createMember,
  getHousehold,
  getHouseholdSettings,
  getMember,
  getMembers,
  lockMember,
  resetMemberPassword,
  unlockMember,
  updateHousehold,
  updateMember,
} from "@/features/households/api";
import type {
  ChangeMemberPermissionsRequest,
  ChangeMemberRoleRequest,
  MemberQuery,
  ResetMemberPasswordRequest,
  UpdateMemberRequest,
} from "@/features/households/types";
import { seasonKeys } from "@/features/seasons/queries";
import { currentUserKeys } from "@/lib/auth/use-current-user";

export const householdKeys = {
  all: ["household"] as const,
  detail: () => [...householdKeys.all, "detail"] as const,
  settings: () => [...householdKeys.all, "settings"] as const,
};

export const memberKeys = {
  all: ["members"] as const,
  list: (query: MemberQuery) => [...memberKeys.all, "list", query] as const,
  detail: (id: string) => [...memberKeys.all, "detail", id] as const,
};

export function useHousehold() {
  return useQuery({ queryKey: householdKeys.detail(), queryFn: getHousehold });
}

export function useUpdateHousehold() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: updateHousehold,
    // Tên hộ còn nằm trong thông tin người đang đăng nhập.
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: householdKeys.all }),
        queryClient.invalidateQueries({ queryKey: currentUserKeys.all }),
      ]),
  });
}

export function useHouseholdSettings() {
  return useQuery({ queryKey: householdKeys.settings(), queryFn: getHouseholdSettings });
}

export function useChangeHouseholdSettings() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: changeHouseholdSettings,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: householdKeys.all }),
  });
}

export function useMembers(query: MemberQuery) {
  return useQuery({
    queryKey: memberKeys.list(query),
    queryFn: () => getMembers(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}

export function useMember(id: string) {
  return useQuery({ queryKey: memberKeys.detail(id), queryFn: () => getMember(id) });
}

// Người trong hộ còn hiện ở trang phân công của mùa vụ: đổi người thì làm mới cả phần đó.
function useInvalidateMembers() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: memberKeys.all }),
      queryClient.invalidateQueries({ queryKey: seasonKeys.all }),
    ]);
}

export function useCreateMember() {
  const invalidate = useInvalidateMembers();
  return useMutation({ mutationFn: createMember, onSuccess: invalidate });
}

export function useUpdateMember(id: string) {
  const invalidate = useInvalidateMembers();
  return useMutation({ mutationFn: (request: UpdateMemberRequest) => updateMember(id, request), onSuccess: invalidate });
}

export function useChangeMemberRole(id: string) {
  const invalidate = useInvalidateMembers();
  return useMutation({
    mutationFn: (request: ChangeMemberRoleRequest) => changeMemberRole(id, request),
    onSuccess: invalidate,
  });
}

export function useChangeMemberPermissions(id: string) {
  const invalidate = useInvalidateMembers();
  return useMutation({
    mutationFn: (request: ChangeMemberPermissionsRequest) => changeMemberPermissions(id, request),
    onSuccess: invalidate,
  });
}

export function useLockMember(id: string) {
  const invalidate = useInvalidateMembers();
  return useMutation({ mutationFn: () => lockMember(id), onSuccess: invalidate });
}

export function useUnlockMember(id: string) {
  const invalidate = useInvalidateMembers();
  return useMutation({ mutationFn: () => unlockMember(id), onSuccess: invalidate });
}

export function useResetMemberPassword(id: string) {
  return useMutation({ mutationFn: (request: ResetMemberPasswordRequest) => resetMemberPassword(id, request) });
}
