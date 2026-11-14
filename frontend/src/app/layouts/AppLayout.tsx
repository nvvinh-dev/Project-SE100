import { CircleUserIcon } from "lucide-react";
import { useState } from "react";
import { Link, Outlet, ScrollRestoration } from "react-router";
import { AppSidebar } from "@/app/layouts/AppSidebar";
import { BottomNav } from "@/app/layouts/BottomNav";
import { QuickAddDialog } from "@/app/layouts/QuickAddDialog";
import { navigationFor } from "@/app/navigation";
import { ForbiddenState } from "@/app/pages/ForbiddenState";
import { useRouteHandle } from "@/app/route-handle";
import { checkAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { cn } from "@/lib/utils";

/**
 * Khung của mọi trang sau khi đăng nhập. Điện thoại: thanh tab dưới (ẩn ở trang form). Máy tính: menu trái 240px,
 * nội dung rộng tối đa 1024px. Quản trị hệ thống chỉ có thanh trên với liên kết tới trang tài khoản.
 */
export function AppLayout() {
  const { data: user } = useCurrentUser();
  const handle = useRouteHandle();
  const [quickAddOpen, setQuickAddOpen] = useState(false);

  // RequireAuth chỉ dựng khung này khi đã có người dùng hiện tại.
  if (!user) {
    return null;
  }

  // Trang không được phép: giữ nguyên đường dẫn, không dựng component trang nên không gọi API của trang.
  const access = handle.access ? checkAccess(user, handle.access) : ({ allowed: true } as const);
  const content = access.allowed ? (
    <Outlet />
  ) : (
    <ForbiddenState role={user.role} missingPermission={access.missingPermission} />
  );

  // Sang trang mới thì cuộn lên đầu trang; quay lại bằng nút Back thì về đúng vị trí cũ.
  const scrollRestoration = <ScrollRestoration />;

  if (user.role === "SystemAdmin") {
    return (
      <>
        {scrollRestoration}
        <header className="border-b">
          <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between px-4">
            <Link to="/admin" className="font-semibold text-primary">
              Sổ Tay Nhà Nông · Quản trị
            </Link>
            <Link to="/account" className="flex min-h-11 items-center gap-2 text-sm">
              <CircleUserIcon className="size-4" aria-hidden />
              Tài khoản
            </Link>
          </div>
        </header>
        <main className="mx-auto w-full max-w-5xl p-4 md:py-6">{content}</main>
      </>
    );
  }

  const navigation = navigationFor(user);
  const openQuickAdd = navigation.quickAddItems.length > 0 ? () => setQuickAddOpen(true) : undefined;
  const showBottomNav = !handle.form;

  return (
    <>
      {scrollRestoration}
      <AppSidebar items={navigation.sidebarItems} groups={navigation.groups} onQuickAdd={openQuickAdd} />
      <div className="md:pl-60">
        <main
          className={cn(
            "mx-auto w-full max-w-5xl p-4 md:py-6",
            // Chừa chỗ cho thanh tab cố định ở đáy (64px cộng vùng an toàn) để nội dung cuối trang không bị che.
            showBottomNav && "pb-[calc(5rem+env(safe-area-inset-bottom))] md:pb-6",
          )}
        >
          {content}
        </main>
      </div>
      {showBottomNav && <BottomNav entries={navigation.tabs} onQuickAdd={() => setQuickAddOpen(true)} />}
      <QuickAddDialog open={quickAddOpen} onOpenChange={setQuickAddOpen} items={navigation.quickAddItems} />
    </>
  );
}
