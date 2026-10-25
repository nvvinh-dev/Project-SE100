import { QueryClient } from "@tanstack/react-query";
import { ApiError } from "@/lib/api-error";

const MAX_RETRIES = 3;

// Lỗi 4xx là kết quả của chính yêu cầu (dữ liệu sai, không có quyền, không tìm thấy): thử lại vẫn ra lỗi đó,
// chỉ làm thông báo hiện chậm vài giây. Chỉ thử lại khi mất mạng hoặc máy chủ lỗi.
function shouldRetry(failureCount: number, error: Error): boolean {
  if (error instanceof ApiError && error.status >= 400 && error.status < 500) {
    return false;
  }
  return failureCount < MAX_RETRIES;
}

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: { retry: shouldRetry },
  },
});
