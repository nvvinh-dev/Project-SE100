import { ChevronLeftIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Link } from "react-router";
import { buttonVariants } from "@/components/ui/button";

interface PageHeaderProps {
  title: ReactNode;
  /** Dòng mô tả ngắn dưới tiêu đề. */
  description?: ReactNode;
  /** Trang chi tiết, form: đường dẫn của nút quay lại. */
  backTo?: string;
  /** Nút chính của trang danh sách ("Thêm thửa đất") hoặc menu `⋯` của trang chi tiết; ẩn khi không có quyền. */
  action?: ReactNode;
}

export function PageHeader({ title, description, backTo, action }: PageHeaderProps) {
  return (
    <header className="flex items-start gap-2">
      {backTo && (
        <Link to={backTo} aria-label="Quay lại" className={buttonVariants({ variant: "ghost", size: "icon", className: "-ml-2" })}>
          <ChevronLeftIcon />
        </Link>
      )}
      <div className="min-w-0 flex-1 self-center">
        <h1 className="text-xl font-semibold break-words md:text-2xl">{title}</h1>
        {description && <p className="mt-1 text-sm text-muted-foreground">{description}</p>}
      </div>
      {action && <div className="shrink-0">{action}</div>}
    </header>
  );
}
