import { NavLink } from "react-router";
import { type NavGroup, type NavItem, quickAddButton } from "@/app/navigation";
import { Button } from "@/components/ui/button";
import { cn } from "@/lib/utils";

interface AppSidebarProps {
  /** Các mục của thanh tab, đã bỏ ô `＋` và mục "Thêm". */
  items: readonly NavItem[];
  /** Các nhóm của trang "Thêm" hiện thẳng ra, đã bỏ nhóm rỗng. */
  groups: readonly NavGroup[];
  /** Không có thì không hiện nút "Ghi nhanh" (Nhân công không có mục ghi nhanh nào). */
  onQuickAdd?: () => void;
}

/** Menu bên trái trên máy tính (từ 768px), rộng 240px. */
export function AppSidebar({ items, groups, onQuickAdd }: AppSidebarProps) {
  return (
    <aside className="fixed inset-y-0 left-0 hidden w-60 flex-col gap-4 overflow-y-auto border-r bg-background p-4 md:flex">
      <p className="px-2 text-lg font-semibold text-primary">Sổ Tay Nhà Nông</p>

      {onQuickAdd && (
        <Button onClick={onQuickAdd} className="h-11 justify-start">
          <quickAddButton.icon data-icon="inline-start" />
          {quickAddButton.label}
        </Button>
      )}

      <nav aria-label="Điều hướng chính" className="flex flex-col gap-4">
        <ul className="flex flex-col gap-1">
          {items.map((item) => (
            <li key={item.to + item.label}>
              <SidebarLink item={item} />
            </li>
          ))}
        </ul>

        {groups.map((group) => (
          <section key={group.title} className="flex flex-col gap-1">
            <h2 className="px-2 text-xs font-medium text-muted-foreground uppercase">{group.title}</h2>
            <ul className="flex flex-col gap-1">
              {group.items.map((item) => (
                <li key={item.to}>
                  <SidebarLink item={item} />
                </li>
              ))}
            </ul>
          </section>
        ))}
      </nav>
    </aside>
  );
}

function SidebarLink({ item }: { item: NavItem }) {
  return (
    <NavLink
      to={item.to}
      end={item.end}
      className={({ isActive }) =>
        cn(
          "flex min-h-11 items-center gap-3 rounded-md px-2 text-sm",
          isActive ? "bg-primary/10 font-medium text-primary" : "text-foreground hover:bg-muted",
        )
      }
    >
      <item.icon className="size-4" aria-hidden />
      {item.label}
    </NavLink>
  );
}
