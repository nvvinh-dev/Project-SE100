import { ShieldXIcon } from "lucide-react";
import { Link } from "react-router";
import { buttonVariants } from "@/components/ui/button";
import { homePathFor } from "@/lib/auth/home-path";
import { extraPermissionLabels } from "@/lib/enum-labels";
import type { ExtraPermission, UserRole } from "@/lib/enums";

interface ForbiddenStateProps {
  role: UserRole;
  /** Thành viên hộ thiếu quyền bổ sung: thêm một dòng để biết cần xin Chủ hộ bật quyền gì. */
  missingPermission: ExtraPermission | null;
}

/** Thay cho nội dung trang khi vai trò hoặc quyền bổ sung không cho mở; đường dẫn giữ nguyên. */
export function ForbiddenState({ role, missingPermission }: ForbiddenStateProps) {
  return (
    <div className="flex flex-col items-center gap-3 px-4 py-16 text-center">
      <ShieldXIcon className="size-10 text-muted-foreground" aria-hidden />
      <p className="font-medium">Tài khoản này không có quyền mở trang này.</p>
      {missingPermission && (
        <p className="text-sm text-muted-foreground">
          Cần quyền {extraPermissionLabels[missingPermission]}, do Chủ hộ bật.
        </p>
      )}
      <Link to={homePathFor(role)} className={buttonVariants({ variant: "outline", className: "mt-2 h-11" })}>
        Về trang chính
      </Link>
    </div>
  );
}
