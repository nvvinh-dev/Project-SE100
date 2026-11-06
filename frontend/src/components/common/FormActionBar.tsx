import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { useNavigate } from "react-router";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { Button } from "@/components/ui/button";

interface FormActionBarProps {
  /** Trang quay về khi bấm "Quay lại" (trang chi tiết, hoặc danh sách khi đang tạo mới). */
  backTo: string;
  /** Đã nhập gì đó: bấm "Quay lại" thì hỏi trước khi bỏ. */
  isDirty: boolean;
  /** Chữ nút lưu nói rõ việc sẽ xảy ra, ví dụ "Lưu thửa đất". */
  submitLabel: string;
  /** Đang gửi: khóa nút lưu và hiện vòng quay. */
  pending: boolean;
}

/**
 * Thanh nút của trang form, đặt cuối thẻ `<form>`: "Quay lại" (viền) bên trái, nút lưu (đầy màu) bên phải.
 * Điện thoại: dính đáy màn hình (trang form ẩn thanh tab). Máy tính: nằm cuối form.
 */
export function FormActionBar({ backTo, isDirty, submitLabel, pending }: FormActionBarProps) {
  const navigate = useNavigate();
  const [confirmOpen, setConfirmOpen] = useState(false);

  function goBack() {
    void navigate(backTo);
  }

  return (
    <>
      {/* Chừa chỗ để ô cuối form không bị thanh nút cố định che trên điện thoại. */}
      <div aria-hidden className="h-[calc(4.5rem+env(safe-area-inset-bottom))] md:hidden" />
      <div className="fixed inset-x-0 bottom-0 z-40 flex gap-3 border-t bg-background p-3 pb-[calc(0.75rem+env(safe-area-inset-bottom))] md:static md:z-auto md:justify-end md:border-0 md:bg-transparent md:p-0">
        <Button
          type="button"
          variant="outline"
          className="flex-1 md:flex-none"
          disabled={pending}
          onClick={() => (isDirty ? setConfirmOpen(true) : goBack())}
        >
          Quay lại
        </Button>
        <Button type="submit" className="flex-1 md:flex-none" disabled={pending}>
          {pending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
          {submitLabel}
        </Button>
      </div>

      <ConfirmDialog
        open={confirmOpen}
        onOpenChange={setConfirmOpen}
        title="Bỏ các thay đổi chưa lưu?"
        description="Dữ liệu vừa nhập trên form này sẽ không được lưu."
        confirmLabel="Bỏ thay đổi"
        destructive
        onConfirm={goBack}
      />
    </>
  );
}
