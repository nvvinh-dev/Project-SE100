import type { ReactNode } from "react";
import { Link, useNavigate } from "react-router";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { cn } from "@/lib/utils";

export interface DataListColumn<T> {
  header: string;
  cell: (item: T) => ReactNode;
  /** Ví dụ `text-right` cho cột số lượng, tiền. */
  className?: string;
}

interface DataListProps<T> {
  items: readonly T[];
  getKey: (item: T) => string;
  /** Đường dẫn trang chi tiết; có thì chạm cả thẻ, bấm cả hàng để mở. */
  getHref?: (item: T) => string;
  /** Thẻ trên điện thoại: dòng chính (đậm). */
  title: (item: T) => ReactNode;
  /** Thẻ trên điện thoại: dòng phụ (xám). */
  subtitle?: (item: T) => ReactNode;
  /** Thẻ trên điện thoại: nhãn trạng thái ở góc phải. */
  badge?: (item: T) => ReactNode;
  /** Cột của bảng, hiện từ màn hình `md`. */
  columns: readonly DataListColumn<T>[];
}

/** Danh sách bản ghi: thẻ trên điện thoại, bảng từ màn hình `md`. Danh sách trống do trang hiện `EmptyState`. */
export function DataList<T>({ items, getKey, getHref, title, subtitle, badge, columns }: DataListProps<T>) {
  const navigate = useNavigate();

  return (
    <>
      <ul className="flex flex-col gap-2 md:hidden">
        {items.map((item) => {
          const href = getHref?.(item);
          const cardClass = "flex min-h-11 items-start gap-3 rounded-lg border bg-card p-3";
          const content = (
            <>
              <div className="min-w-0 flex-1">
                <p className="font-medium break-words">{title(item)}</p>
                {subtitle && <div className="mt-0.5 text-sm text-muted-foreground">{subtitle(item)}</div>}
              </div>
              {badge && <div className="shrink-0">{badge(item)}</div>}
            </>
          );
          return (
            <li key={getKey(item)}>
              {href ? (
                <Link to={href} className={cn(cardClass, "active:bg-muted")}>
                  {content}
                </Link>
              ) : (
                <div className={cardClass}>{content}</div>
              )}
            </li>
          );
        })}
      </ul>

      <div className="hidden rounded-lg border md:block">
        <Table>
          <TableHeader>
            <TableRow>
              {columns.map((column, index) => (
                <TableHead key={index} className={column.className}>
                  {column.header}
                </TableHead>
              ))}
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => {
              const href = getHref?.(item);
              return (
                <TableRow
                  key={getKey(item)}
                  className={cn(href && "cursor-pointer")}
                  onClick={href ? () => void navigate(href) : undefined}
                >
                  {columns.map((column, index) => (
                    <TableCell key={index} className={column.className}>
                      {/* Ô đầu là liên kết để mở được bằng bàn phím; bấm chỗ khác trên hàng do onClick của hàng xử lý. */}
                      {index === 0 && href ? (
                        <Link to={href} className="font-medium hover:underline" onClick={(event) => event.stopPropagation()}>
                          {column.cell(item)}
                        </Link>
                      ) : (
                        column.cell(item)
                      )}
                    </TableCell>
                  ))}
                </TableRow>
              );
            })}
          </TableBody>
        </Table>
      </div>
    </>
  );
}
