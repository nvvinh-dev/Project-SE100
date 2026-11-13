import { EllipsisVerticalIcon, KeyRoundIcon, LockIcon, LockOpenIcon } from "lucide-react";
import { useState } from "react";
import { useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showSuccess } from "@/components/common/toast";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { MemberInfoForm } from "@/features/households/components/MemberInfoForm";
import { MemberPermissionsSection } from "@/features/households/components/MemberPermissionsSection";
import { MemberRoleSection } from "@/features/households/components/MemberRoleSection";
import { ResetPasswordDialog } from "@/features/households/components/ResetPasswordDialog";
import { useLockMember, useMember, useUnlockMember } from "@/features/households/queries";
import type { MemberResponse } from "@/features/households/types";
import { ApiError } from "@/lib/api-error";
import { userRoleLabels } from "@/lib/enum-labels";

type Dialog = "lock" | "unlock" | "reset-password" | null;

/**
 * `/members/:id`: chi tiết một người trong hộ, chỉ Chủ hộ (mẫu trang chi tiết). Sửa thông tin, đổi vai trò, quyền bổ sung
 * ngay trên trang; đặt lại mật khẩu, khóa, mở khóa trong menu ⋯.
 */
export function MemberDetailPage() {
  const { id = "" } = useParams();
  const member = useMember(id);

  if (member.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (member.isError) {
    // BR-GEN-02: người của hộ khác cũng trả NOT_FOUND, hiện như không tồn tại.
    return member.error instanceof ApiError && member.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={member.error} onRetry={() => void member.refetch()} />
    );
  }

  // Dựng lại các khối sau mỗi lần lưu hoặc tải lại, để giá trị và version luôn là bản mới nhất.
  return <MemberDetail key={`${member.data.id}:${member.data.version}`} member={member.data} onReload={() => void member.refetch()} />;
}

function MemberDetail({ member, onReload }: { member: MemberResponse; onReload: () => void }) {
  const [dialog, setDialog] = useState<Dialog>(null);
  const lockMember = useLockMember(member.id);
  const unlockMember = useUnlockMember(member.id);
  const [actionError, setActionError] = useState<unknown>(null);
  // BR-USR-07: tài khoản Chủ hộ không sửa, khóa, đặt lại mật khẩu ở đây; Chủ hộ sửa thông tin của mình ở trang Tài khoản.
  const isOwner = member.role === "Owner";

  function runLock(lock: boolean) {
    setActionError(null);
    const mutation = lock ? lockMember : unlockMember;
    mutation.mutate(undefined, {
      onSuccess: () => {
        setDialog(null);
        showSuccess(lock ? `Đã khóa tài khoản ${member.fullName}.` : `Đã mở khóa tài khoản ${member.fullName}.`);
      },
      onError: (error) => {
        setDialog(null);
        setActionError(error);
      },
    });
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={member.fullName}
        description={member.phone}
        backTo="/members"
        action={
          !isOwner && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                  <EllipsisVerticalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem className="min-h-11" onSelect={() => setDialog("reset-password")}>
                  <KeyRoundIcon />
                  Đặt lại mật khẩu
                </DropdownMenuItem>
                {member.isLocked ? (
                  <DropdownMenuItem className="min-h-11" onSelect={() => setDialog("unlock")}>
                    <LockOpenIcon />
                    Mở khóa
                  </DropdownMenuItem>
                ) : (
                  <>
                    {/* Thao tác khóa dữ liệu để cuối menu, chữ đỏ. */}
                    <DropdownMenuSeparator />
                    <DropdownMenuItem variant="destructive" className="min-h-11" onSelect={() => setDialog("lock")}>
                      <LockIcon />
                      Khóa tài khoản
                    </DropdownMenuItem>
                  </>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          )
        }
      />

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{userRoleLabels[member.role]}</Badge>
        {member.isLocked && <StatusBadge kind="Locked" />}
      </div>

      {actionError !== null && <ErrorAlert error={actionError} />}

      {isOwner ? (
        <p className="text-sm text-muted-foreground">
          Đây là tài khoản Chủ hộ. Chủ hộ sửa họ tên, số điện thoại, mật khẩu của mình ở trang Tài khoản.
        </p>
      ) : (
        <>
          <section className="flex flex-col gap-4 rounded-lg border p-4">
            <h2 className="font-semibold">Thông tin</h2>
            <MemberInfoForm member={member} onReload={onReload} />
          </section>

          <section className="flex flex-col gap-4 rounded-lg border p-4">
            <MemberRoleSection member={member} onReload={onReload} />
          </section>

          <section className="flex flex-col gap-4 rounded-lg border p-4">
            <h2 className="font-semibold">Quyền bổ sung</h2>
            <MemberPermissionsSection member={member} onReload={onReload} />
          </section>
        </>
      )}

      <ConfirmDialog
        open={dialog === "lock"}
        onOpenChange={(open) => setDialog(open ? "lock" : null)}
        title={`Khóa tài khoản ${member.fullName}?`}
        description="Thành viên bị đăng xuất ngay trên mọi thiết bị và không đăng nhập được cho tới khi được mở khóa. Dữ liệu, lịch sử của thành viên vẫn giữ nguyên."
        confirmLabel="Khóa tài khoản"
        destructive
        onConfirm={() => runLock(true)}
        pending={lockMember.isPending}
      />
      <ConfirmDialog
        open={dialog === "unlock"}
        onOpenChange={(open) => setDialog(open ? "unlock" : null)}
        title={`Mở khóa tài khoản ${member.fullName}?`}
        description="Thành viên đăng nhập lại được bằng mật khẩu hiện tại."
        confirmLabel="Mở khóa"
        onConfirm={() => runLock(false)}
        pending={unlockMember.isPending}
      />
      <ResetPasswordDialog
        memberId={member.id}
        memberName={member.fullName}
        open={dialog === "reset-password"}
        onOpenChange={(open) => setDialog(open ? "reset-password" : null)}
      />
    </div>
  );
}
