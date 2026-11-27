// Trùng tên DTO backend (Features/Ai/AiDtos.cs).

/** Số lượt AI của hộ trong ngày hôm nay theo giờ Việt Nam. */
export interface AIQuotaResponse {
  usageDate: string;
  used: number;
  limit: number;
  remaining: number;
}

/** Một cặp câu hỏi, câu trả lời trước đó trong phiên trò chuyện; hội thoại chỉ giữ trên trang, không lưu ở máy chủ. */
export interface AssistantExchange {
  question: string;
  answer: string;
}

export interface AskAssistantRequest {
  question: string;
  history: AssistantExchange[];
}

export interface AskAssistantResponse {
  answer: string;
  /** Số lượt sau khi đã tính lượt này. */
  quota: AIQuotaResponse;
}

/** Giới hạn độ dài câu hỏi, khớp với kiểm tra của máy chủ. */
export const ASSISTANT_QUESTION_MAX_LENGTH = 500;

/** State điều hướng khi mở `/ai` kèm câu hỏi, ví dụ từ khối "Hỏi trợ lý" của trang Hôm nay. */
export interface AssistantLocationState {
  question: string;
}
