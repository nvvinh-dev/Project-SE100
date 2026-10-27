import { InboxIcon, SearchXIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Button } from "@/components/ui/button";

type EmptyStateProps =
  | {
      /** Một câu giải thích, ví dụ "Chưa có mùa vụ nào. Tạo mùa vụ đầu tiên để bắt đầu theo dõi." */
      message: string;
      /** Nút tạo mới; trang chỉ truyền khi người dùng có quyền. */
      action?: ReactNode;
      onClearFilters?: never;
    }
  | {
      /** Danh sách trống vì đang lọc: hiện "Không có kết quả phù hợp." và nút "Bỏ lọc". */
      onClearFilters: () => void;
      message?: never;
      action?: never;
    };

/** Danh sách trống, hoặc lọc không ra kết quả. */
export function EmptyState(props: EmptyStateProps) {
  const filtered = props.onClearFilters !== undefined;
  const Icon = filtered ? SearchXIcon : InboxIcon;

  return (
    <div className="flex flex-col items-center gap-3 rounded-lg border border-dashed px-4 py-10 text-center">
      <Icon className="size-8 text-muted-foreground" aria-hidden />
      <p className="max-w-sm text-sm text-muted-foreground">{filtered ? "Không có kết quả phù hợp." : props.message}</p>
      {filtered ? (
        <Button variant="outline" onClick={props.onClearFilters}>
          Bỏ lọc
        </Button>
      ) : (
        props.action
      )}
    </div>
  );
}
