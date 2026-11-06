import { NavLink } from "react-router";
import { type NavItem, QUICK_ADD, quickAddButton, type TabEntry } from "@/app/navigation";
import { cn } from "@/lib/utils";

interface BottomNavProps {
  /** Các ô của thanh tab người dùng được thấy, đúng thứ tự. */
  entries: readonly TabEntry[];
  onQuickAdd: () => void;
}

/** Thanh tab dưới cùng trên điện thoại (dưới 768px), cao 64px cộng vùng an toàn của màn hình tai thỏ. */
export function BottomNav({ entries, onQuickAdd }: BottomNavProps) {
  return (
    <nav
      aria-label="Điều hướng chính"
      className="fixed inset-x-0 bottom-0 z-40 border-t bg-background pb-[env(safe-area-inset-bottom)] md:hidden"
    >
      <ul className="flex h-16">
        {entries.map((entry) =>
          entry === QUICK_ADD ? (
            <li key={QUICK_ADD} className="flex flex-1 items-center justify-center">
              <button
                type="button"
                onClick={onQuickAdd}
                aria-label={quickAddButton.label}
                className="flex size-12 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-md"
              >
                <quickAddButton.icon className="size-6" aria-hidden />
              </button>
            </li>
          ) : (
            <li key={entry.to + entry.label} className="flex flex-1">
              <TabLink item={entry} />
            </li>
          ),
        )}
      </ul>
    </nav>
  );
}

function TabLink({ item }: { item: NavItem }) {
  return (
    <NavLink
      to={item.to}
      end={item.end}
      className={({ isActive }) =>
        cn(
          "flex flex-1 flex-col items-center justify-center gap-1 text-xs",
          isActive ? "font-medium text-primary" : "text-muted-foreground",
        )
      }
    >
      <item.icon className="size-5" aria-hidden />
      {item.label}
    </NavLink>
  );
}
