import { CircleAlertIcon, RotateCwIcon } from "lucide-react";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { getErrorMessage } from "@/lib/api-error";

interface ErrorAlertProps {
  /** Lỗi bắt được, thường là `error` của `useQuery`; hiện nguyên văn câu backend viết. */
  error: unknown;
  /** Có thì hiện nút thử lại, ví dụ `refetch` của `useQuery`. */
  onRetry?: () => void;
  /** Ví dụ "Tải lại" khi dữ liệu đã bị người khác thay đổi. */
  retryLabel?: string;
}

/** Khung báo lỗi khi tải dữ liệu hoặc khi gửi form thất bại. */
export function ErrorAlert({ error, onRetry, retryLabel = "Thử lại" }: ErrorAlertProps) {
  return (
    <Alert variant="destructive">
      <CircleAlertIcon />
      <AlertTitle className="leading-snug">{getErrorMessage(error)}</AlertTitle>
      {onRetry && (
        <AlertDescription>
          <Button variant="outline" className="mt-2" onClick={onRetry}>
            <RotateCwIcon data-icon="inline-start" />
            {retryLabel}
          </Button>
        </AlertDescription>
      )}
    </Alert>
  );
}
