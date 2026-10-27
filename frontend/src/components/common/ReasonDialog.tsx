import { Loader2Icon } from "lucide-react";
import { type ReactNode, useId, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ApiError, getErrorMessage } from "@/lib/api-error";
import { cn } from "@/lib/utils";

interface ReasonDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Ví dụ "Hủy phiếu thu hoạch". */
  title: string;
  /** Câu nói rõ hậu quả. */
  description?: ReactNode;
  /** Chữ trên nút mang đúng tên thao tác ("Hủy phiếu", "Mở lại mùa vụ"). */
  confirmLabel: string;
  /** Nhận lý do đã bỏ khoảng trắng hai đầu, không bao giờ rỗng. */
  onConfirm: (reason: string) => void;
  /** Đang gửi: khóa nút và không cho đóng hộp thoại. Trang tự đóng khi gửi thành công. */
  pending?: boolean;
  /** Lỗi của lần gửi gần nhất; hiện trong hộp thoại, giữ nguyên lý do đã nhập. */
  error?: unknown;
  /** Thao tác làm mất, khóa dữ liệu: nút màu đỏ. */
  destructive?: boolean;
}

/** Hộp thoại nhập lý do bắt buộc cho `Correct`, `Void`, hủy mùa vụ, `Adjust`, `Reopen` (BR-GEN-06). */
export function ReasonDialog({ open, onOpenChange, pending = false, ...formProps }: ReasonDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      {/* Không có câu mô tả thì khai báo rõ để Radix không cảnh báo thiếu DialogDescription. */}
      <DialogContent showCloseButton={false} {...(formProps.description ? {} : { "aria-describedby": undefined })}>
        {/* Nội dung hộp thoại dựng lại mỗi lần mở, nên lý do của lần trước không còn. */}
        <ReasonForm {...formProps} pending={pending} onBack={() => onOpenChange(false)} />
      </DialogContent>
    </Dialog>
  );
}

type ReasonFormProps = Omit<ReasonDialogProps, "open" | "onOpenChange"> & { onBack: () => void };

function ReasonForm({
  title,
  description,
  confirmLabel,
  onConfirm,
  onBack,
  pending = false,
  error,
  destructive = false,
}: ReasonFormProps) {
  const reasonId = useId();
  const [reason, setReason] = useState("");
  const [submitted, setSubmitted] = useState(false);

  const trimmed = reason.trim();
  const serverFieldError = error instanceof ApiError ? error.fieldErrors.reason?.[0] : undefined;
  const fieldError = submitted && !trimmed ? "Cần nhập lý do." : serverFieldError;
  // Lỗi của riêng ô lý do đã hiện dưới ô, không hiện thêm câu chung.
  const formError = error && !serverFieldError ? getErrorMessage(error) : null;

  return (
    <form
      className="grid gap-4"
      onSubmit={(event) => {
        event.preventDefault();
        setSubmitted(true);
        if (trimmed) {
          onConfirm(trimmed);
        }
      }}
    >
      <DialogHeader>
        <DialogTitle>{title}</DialogTitle>
        {description && <DialogDescription>{description}</DialogDescription>}
      </DialogHeader>
      <div className="grid gap-2">
        <Label htmlFor={reasonId}>
          Lý do <span className="text-destructive">*</span>
        </Label>
        <Textarea
          id={reasonId}
          value={reason}
          onChange={(event) => setReason(event.target.value)}
          aria-invalid={fieldError ? true : undefined}
          aria-describedby={fieldError ? `${reasonId}-error` : undefined}
        />
        {fieldError && (
          <p id={`${reasonId}-error`} className="text-sm text-destructive">
            {fieldError}
          </p>
        )}
      </div>
      {formError && (
        <p role="alert" className="text-sm text-destructive">
          {formError}
        </p>
      )}
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onBack} disabled={pending}>
          Quay lại
        </Button>
        <Button
          type="submit"
          disabled={pending}
          className={cn(destructive && "bg-destructive text-white hover:bg-destructive/90")}
        >
          {pending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
          {confirmLabel}
        </Button>
      </DialogFooter>
    </form>
  );
}
