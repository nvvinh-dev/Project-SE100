import { ChevronLeftIcon, ChevronRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import type { PageMeta } from "@/lib/api-client";

interface PaginationProps {
  /** `meta` của phản hồi API danh sách. */
  meta: PageMeta;
  /** Trang lưu trên đường dẫn (`?page=2`) để quay lại vẫn giữ nguyên. */
  onPageChange: (page: number) => void;
}

/** Nút "Trang trước", "Trang sau" và số trang hiện tại; chỉ có một trang thì không hiện. */
export function Pagination({ meta, onPageChange }: PaginationProps) {
  const totalPages = Math.max(1, Math.ceil(meta.totalCount / meta.pageSize));
  if (totalPages <= 1) {
    return null;
  }

  return (
    <nav aria-label="Phân trang" className="flex items-center justify-between gap-2 md:justify-end">
      <Button variant="outline" disabled={meta.page <= 1} onClick={() => onPageChange(meta.page - 1)}>
        <ChevronLeftIcon data-icon="inline-start" />
        Trang trước
      </Button>
      <span className="text-sm whitespace-nowrap text-muted-foreground">
        Trang {meta.page}/{totalPages}
      </span>
      <Button variant="outline" disabled={meta.page >= totalPages} onClick={() => onPageChange(meta.page + 1)}>
        Trang sau
        <ChevronRightIcon data-icon="inline-end" />
      </Button>
    </nav>
  );
}
