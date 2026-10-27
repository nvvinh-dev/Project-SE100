import { Loader2Icon } from "lucide-react";
import type { ReactNode } from "react";
import {
  AlertDialog,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Button } from "@/components/ui/button";
import type { ConfirmationItem } from "@/lib/api-error";
import { cn } from "@/lib/utils";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Câu hỏi, ví dụ "Hủy công việc này?". */
  title: string;
  /** Câu nói rõ hậu quả; với `CONFIRMATION_REQUIRED` là nguyên văn câu backend gửi. */
  description: ReactNode;
  /** Bản ghi bị ảnh hưởng (`details.items`), đọc bằng `getConfirmationItems`. */
  items?: readonly ConfirmationItem[];
  /** Chữ trên nút mang đúng tên thao tác ("Hủy công việc", "Chốt mùa vụ"), không dùng "Có", "Đồng ý". */
  confirmLabel: string;
  onConfirm: () => void;
  /** Đang gửi: khóa nút và không cho đóng hộp thoại. Trang tự đóng khi gửi thành công. */
  pending?: boolean;
  /** Thao tác làm mất, khóa dữ liệu: nút màu đỏ. */
  destructive?: boolean;
}

/** Hộp thoại xác nhận thao tác không đảo ngược và xác nhận theo `CONFIRMATION_REQUIRED` (BR-GEN-14). */
export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  items = [],
  confirmLabel,
  onConfirm,
  pending = false,
  destructive = false,
}: ConfirmDialogProps) {
  return (
    <AlertDialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          <AlertDialogDescription>{description}</AlertDialogDescription>
        </AlertDialogHeader>
        {items.length > 0 && (
          <ul className="max-h-48 list-disc overflow-y-auto pl-5 text-sm">
            {items.map((item) => (
              <li key={item.id}>{item.label}</li>
            ))}
          </ul>
        )}
        <AlertDialogFooter>
          <AlertDialogCancel disabled={pending}>Quay lại</AlertDialogCancel>
          {/* Không dùng AlertDialogAction vì nút đó tự đóng hộp thoại trước khi biết gửi thành công hay không. */}
          <Button
            onClick={onConfirm}
            disabled={pending}
            className={cn(destructive && "bg-destructive text-white hover:bg-destructive/90")}
          >
            {pending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
            {confirmLabel}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
