import { ChevronRightIcon } from "lucide-react";
import { Link } from "react-router";
import type { NavItem } from "@/app/navigation";
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from "@/components/ui/dialog";

interface QuickAddDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Các nút người dùng được thấy, đã lọc theo quyền. */
  items: readonly NavItem[];
}

/** Bảng ghi nhanh "＋": mỗi nút lớn một dòng, đi thẳng tới form. */
export function QuickAddDialog({ open, onOpenChange, items }: QuickAddDialogProps) {
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
                  to={item.to}
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
