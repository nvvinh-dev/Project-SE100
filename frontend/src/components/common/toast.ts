import { toast } from "sonner";
import type { ResponseWarning } from "@/lib/api-client";
import { getErrorMessage } from "@/lib/api-error";

// Thành công, cảnh báo tự tắt; lỗi để người dùng tự tắt để kịp đọc hết câu.
const SUCCESS_DURATION_MS = 3_000;
const WARNING_DURATION_MS = 6_000;

/** Thông báo ngắn nền xanh, ví dụ "Đã lưu thửa đất.". */
export function showSuccess(message: string) {
  toast.success(message, { duration: SUCCESS_DURATION_MS });
}

/** Thông báo ngắn nền vàng cam. */
export function showWarning(message: string) {
  toast.warning(message, { duration: WARNING_DURATION_MS });
}

/** Hiện từng `warnings` của phản hồi thành công, mỗi cảnh báo một thông báo. */
export function showWarnings(warnings: readonly ResponseWarning[]) {
  for (const warning of warnings) {
    showWarning(warning.message);
  }
}

/** Thông báo ngắn nền đỏ với nguyên văn câu lỗi của backend; người dùng tự tắt. */
export function showError(error: unknown) {
  toast.error(getErrorMessage(error), {
    duration: Number.POSITIVE_INFINITY,
    closeButton: true,
  });
}
