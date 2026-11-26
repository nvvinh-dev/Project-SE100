import { CircleAlertIcon, RotateCwIcon } from "lucide-react";
import type { AIBlock } from "@/features/ai/ai-availability";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";

interface AIBlockedAlertProps {
  block: AIBlock;
  /** Dịch vụ AI lỗi thì cho thử lại; hết lượt thì không, vì phải chờ sang ngày mới. */
  onRetry?: () => void;
}

/** Lý do tính năng AI tạm thời không dùng được, đặt ngay trên ô nhập đang bị khóa (FR-AI-05). */
export function AIBlockedAlert({ block, onRetry }: AIBlockedAlertProps) {
  return (
    <Alert>
      <CircleAlertIcon />
      <AlertTitle className="leading-snug whitespace-normal">{block.message}</AlertTitle>
      {onRetry && block.code === "AI_UNAVAILABLE" && (
        <AlertDescription>
          <Button type="button" variant="outline" className="mt-2" onClick={onRetry}>
            <RotateCwIcon data-icon="inline-start" />
            Thử lại
          </Button>
        </AlertDescription>
      )}
    </Alert>
  );
}
