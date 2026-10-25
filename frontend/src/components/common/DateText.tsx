import { formatDate, formatDateTime } from "@/lib/format";
import { cn } from "@/lib/utils";

interface DateTextProps {
  /** Ngày nghiệp vụ `yyyy-MM-dd` (trường `...Date`), hoặc thời điểm ISO 8601 (trường `...At`) khi có `withTime`. */
  value: string;
  /** Hiện cả giờ theo giờ Việt Nam: `26/09/2026 14:30`. */
  withTime?: boolean;
  className?: string;
}

/** Ngày dạng `26/09/2026`. */
export function DateText({ value, withTime = false, className }: DateTextProps) {
  return (
    <time dateTime={value} className={cn("whitespace-nowrap tabular-nums", className)}>
      {withTime ? formatDateTime(value) : formatDate(value)}
    </time>
  );
}
