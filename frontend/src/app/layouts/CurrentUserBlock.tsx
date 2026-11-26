import { CircleUserIcon, Loader2Icon, LogOutIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import { showError } from "@/components/common/toast";
import { useLogout } from "@/features/auth/queries";
import type { CurrentUserResponse } from "@/lib/auth/types";
import { userRoleLabels } from "@/lib/enum-labels";
import { cn } from "@/lib/utils";

interface CurrentUserBlockProps {
  user: CurrentUserResponse;
  className?: string;
}

/**
 * Người đang đăng nhập: họ tên, vai trò, nút "Đăng xuất". Đặt ở cuối menu trái và đầu trang "Thêm"
 * để máy dùng chung trong hộ dễ đổi người, và người dùng biết mình đang là ai.
 */
export function CurrentUserBlock({ user, className }: CurrentUserBlockProps) {
  const logout = useLogout();

  return (
    <section aria-label="Tài khoản đang đăng nhập" className={cn("flex flex-wrap items-center gap-3 rounded-lg border p-3", className)}>
      <CircleUserIcon className="size-8 shrink-0 text-muted-foreground" aria-hidden />
      <div className="min-w-32 flex-1">
        <p className="truncate font-medium">{user.fullName}</p>
        <p className="truncate text-sm text-muted-foreground">{userRoleLabels[user.role]}</p>
      </div>
      <Button
        type="button"
        variant="outline"
        size="sm"
        className="h-11 shrink-0 md:h-8"
        disabled={logout.isPending}
        onClick={() => logout.mutate(undefined, { onError: showError })}
      >
        {logout.isPending ? (
          <Loader2Icon className="animate-spin" data-icon="inline-start" />
        ) : (
          <LogOutIcon data-icon="inline-start" />
        )}
        Đăng xuất
      </Button>
    </section>
  );
}
