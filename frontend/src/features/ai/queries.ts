import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { askAssistant, getAIQuota } from "@/features/ai/api";
import { ApiError } from "@/lib/api-error";

export const aiKeys = {
  all: ["ai"] as const,
  quota: () => [...aiKeys.all, "quota"] as const,
};

/** Số lượt AI còn lại hôm nay, dùng chung cho mọi tính năng AI. */
export function useAIQuota() {
  return useQuery({ queryKey: aiKeys.quota(), queryFn: getAIQuota });
}

export function useAskAssistant() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: askAssistant,
    // Phản hồi đã kèm số lượt sau lượt này, không cần gọi lại API số lượt.
    onSuccess: (response) => queryClient.setQueryData(aiKeys.quota(), response.quota),
    onError: (error) => {
      // Hết lượt thì số lượt trên trang đã cũ; tải lại để dòng "Còn x/y lượt" khớp với máy chủ.
      if (error instanceof ApiError && error.code === "AI_QUOTA_EXCEEDED") {
        void queryClient.invalidateQueries({ queryKey: aiKeys.quota() });
      }
    },
  });
}
