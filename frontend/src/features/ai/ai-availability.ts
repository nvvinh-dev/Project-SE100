import type { AIQuotaResponse } from "@/features/ai/types";
import { ApiError } from "@/lib/api-error";

/** Tính năng AI đang bị khóa vì sao: hết lượt trong ngày, hoặc dịch vụ AI không phản hồi (FR-AI-05). */
export interface AIBlock {
  code: "AI_QUOTA_EXCEEDED" | "AI_UNAVAILABLE";
  message: string;
}

/**
 * Lý do khóa ô nhập của một tính năng AI; `null` khi còn dùng được. Lỗi từ máy chủ hiện nguyên văn câu backend viết.
 * Dùng chung cho mọi tính năng AI.
 */
export function getAIBlock(quota: AIQuotaResponse | undefined, error: unknown): AIBlock | null {
  if (error instanceof ApiError && (error.code === "AI_QUOTA_EXCEEDED" || error.code === "AI_UNAVAILABLE")) {
    return { code: error.code, message: error.message };
  }

  if (quota !== undefined && quota.remaining <= 0) {
    return {
      code: "AI_QUOTA_EXCEEDED",
      message: `Hộ đã dùng hết ${quota.limit} lượt AI hôm nay. Tính năng AI mở lại vào ngày mai; các chức năng khác vẫn dùng bình thường.`,
    };
  }

  return null;
}
