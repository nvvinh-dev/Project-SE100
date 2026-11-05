import { useQuery } from "@tanstack/react-query";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";
import { useAuth } from "@/lib/auth/auth-context";
import type { CurrentUserResponse } from "@/lib/auth/types";

export const currentUserKeys = {
  all: ["current-user"] as const,
};

// Vai trò, quyền đổi ít; khi API báo FORBIDDEN thì nạp lại ngay, không cần tải lại liên tục.
const CURRENT_USER_STALE_TIME_MS = 5 * 60 * 1000;

async function getCurrentUser(): Promise<CurrentUserResponse> {
  const response = await apiClient.get<ResponseEnvelope<CurrentUserResponse>>("/auth/me");
  return response.data.data;
}

/**
 * Người đang đăng nhập. Mọi chỗ ẩn, hiện theo vai trò và quyền bổ sung đọc từ hook này;
 * backend vẫn là nơi quyết định (NFR-04).
 */
export function useCurrentUser() {
  const { status } = useAuth();
  return useQuery({
    queryKey: currentUserKeys.all,
    queryFn: getCurrentUser,
    enabled: status === "authenticated",
    staleTime: CURRENT_USER_STALE_TIME_MS,
  });
}
