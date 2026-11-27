import { ChevronRightIcon } from "lucide-react";
import { Link, useMatch } from "react-router";
import type { NavItem } from "@/app/navigation";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";

interface QuickAddDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Các nút người dùng được thấy, đã lọc theo quyền. */
  items: readonly NavItem[];
}

// Các nút ghi dữ liệu thuộc một mùa vụ: mở bảng từ trang mùa vụ thì chọn sẵn mùa vụ đó, khỏi chọn lại.
const SEASON_SCOPED_PATHS = ["/tasks", "/harvests/new", "/sales/new", "/expenses/new"];

function withSeason(to: string, seasonId: string | undefined): string {
  if (!seasonId || !SEASON_SCOPED_PATHS.includes(to.split("?")[0])) {
    return to;
  }
  return `${to}${to.includes("?") ? "&" : "?"}seasonId=${encodeURIComponent(seasonId)}`;
}

/** Bảng ghi nhanh "＋": mỗi nút lớn một dòng, đi thẳng tới form. */
export function QuickAddDialog({ open, onOpenChange, items }: QuickAddDialogProps) {
  // "/seasons/new" cũng khớp mẫu này nhưng chưa có mùa vụ nào để chọn sẵn.
  const seasonMatch = useMatch("/seasons/:id/*");
  const seasonId = seasonMatch?.params.id === "new" ? undefined : seasonMatch?.params.id;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="gap-4">
        <DialogHeader>
          <DialogTitle>Ghi nhanh</DialogTitle>
          <DialogDescription className="sr-only">Chọn việc cần ghi.</DialogDescription>
        </DialogHeader>
        <nav aria-label="Ghi nhanh">
          <ul className="flex flex-col gap-2">
            {items.map((item) => (
              <li key={item.to}>
                <Link
                  to={withSeason(item.to, seasonId)}
                  onClick={() => onOpenChange(false)}
                  className="flex min-h-14 items-center gap-3 rounded-lg border px-4 font-medium hover:bg-muted"
                >
                  <item.icon className="size-5 text-primary" aria-hidden />
                  <span className="flex-1">{item.label}</span>
                  <ChevronRightIcon className="size-4 text-muted-foreground" aria-hidden />
                </Link>
              </li>
            ))}
          </ul>
        </nav>
      </DialogContent>
    </Dialog>
  );
}
