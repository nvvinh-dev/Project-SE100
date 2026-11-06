import { FileQuestionIcon } from "lucide-react";
import { useNavigate } from "react-router";
import { Button } from "@/components/ui/button";

/** Đường dẫn không khớp trang nào (`*`), theo mẫu "Không tìm thấy" của trang lỗi. */
export function NotFoundPage() {
  const navigate = useNavigate();

  // React Router ghi vị trí trong lịch sử vào history.state.idx; mở thẳng đường dẫn thì không có trang trước để quay lại.
  const canGoBack = ((window.history.state as { idx?: number } | null)?.idx ?? 0) > 0;

  return (
    <div className="flex flex-col items-center gap-3 px-4 py-16 text-center">
      <FileQuestionIcon className="size-10 text-muted-foreground" aria-hidden />
      <p className="max-w-sm font-medium">Không tìm thấy dữ liệu này, có thể đã bị xóa hoặc bạn không có quyền xem.</p>
      <Button
        variant="outline"
        className="mt-2 h-11"
        onClick={() => void (canGoBack ? navigate(-1) : navigate("/", { replace: true }))}
      >
        {canGoBack ? "Về trang trước" : "Về trang chính"}
      </Button>
    </div>
  );
}
