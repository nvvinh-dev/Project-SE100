import { KeyRoundIcon, LockIcon, LockOpenIcon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showSuccess } from "@/components/common/toast";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { useAdminAccount, useLockAccount, useResetAccountPassword, useUnlockAccount } from "@/features/admin/queries";
import type { AdminAccountResponse } from "@/features/admin/types";
import { ResetPasswordDialog } from "@/features/households/components/ResetPasswordDialog";
import { ApiError } from "@/lib/api-error";
import { userRoleLabels } from "@/lib/enum-labels";

type Dialog = "lock" | "unlock" | "reset-password" | null;

/**
 * `/admin/accounts/:id`: chi tiết một tài khoản, chỉ Quản trị. Khóa, mở khóa (FR-ADM-02); đặt lại mật khẩu chỉ với tài khoản
 * Chủ hộ (FR-ADM-03).
 */
export function AdminAccountDetailPage() {
  const { id = "" } = useParams();
  const account = useAdminAccount(id);

  if (account.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (account.isError) {
    // BR-USR-06: tài khoản Quản trị, id không có thật đều trả NOT_FOUND.
    return account.error instanceof ApiError && account.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={account.error} onRetry={() => void account.refetch()} />
    );
  }

  return <AccountDetail account={account.data} />;
}

function AccountDetail({ account }: { account: AdminAccountResponse }) {
  const [dialog, setDialog] = useState<Dialog>(null);
  const [actionError, setActionError] = useState<unknown>(null);
  const lockAccount = useLockAccount(account.id);
  const unlockAccount = useUnlockAccount(account.id);
  const resetPassword = useResetAccountPassword(account.id);

  function runLock(lock: boolean) {
    setActionError(null);
    const mutation = lock ? lockAccount : unlockAccount;
    mutation.mutate(undefined, {
      onSuccess: () => {
        setDialog(null);
        showSuccess(lock ? `Đã khóa tài khoản ${account.fullName}.` : `Đã mở khóa tài khoản ${account.fullName}.`);
      },
      onError: (error) => {
        setDialog(null);
        setActionError(error);
      },
    });
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={account.fullName} description={account.phone} backTo="/admin" />

      <div className="flex flex-wrap gap-2">
        <Badge variant="secondary">{userRoleLabels[account.role]}</Badge>
        {account.isLocked && <StatusBadge kind="Locked" />}
      </div>

      <section className="rounded-lg border p-4">
        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Số điện thoại">{account.phone}</InfoRow>
          <InfoRow label="Vai trò">{userRoleLabels[account.role]}</InfoRow>
          <InfoRow label="Mã hộ">{account.householdCode}</InfoRow>
          <InfoRow label="Tên hộ">{account.householdName}</InfoRow>
          <InfoRow label="Thời điểm tạo tài khoản">
            <DateText value={account.createdAt} withTime />
          </InfoRow>
        </dl>
      </section>

      {actionError !== null && <ErrorAlert error={actionError} />}

      <div className="flex flex-wrap gap-3">
        {/* FR-ADM-03: thành viên quên mật khẩu do Chủ hộ đặt lại, Quản trị chỉ đặt lại cho Chủ hộ. */}
        {account.role === "Owner" && (
          <Button type="button" variant="outline" onClick={() => setDialog("reset-password")}>
            <KeyRoundIcon data-icon="inline-start" />
            Đặt lại mật khẩu
          </Button>
        )}
        {account.isLocked ? (
          <Button type="button" variant="outline" onClick={() => setDialog("unlock")}>
            <LockOpenIcon data-icon="inline-start" />
            Mở khóa
          </Button>
        ) : (
          <Button type="button" variant="destructive" onClick={() => setDialog("lock")}>
            <LockIcon data-icon="inline-start" />
            Khóa tài khoản
          </Button>
        )}
      </div>

      <ConfirmDialog
        open={dialog === "lock"}
        onOpenChange={(open) => setDialog(open ? "lock" : null)}
        title={`Khóa tài khoản ${account.fullName}?`}
        description="Tài khoản bị đăng xuất ngay trên mọi thiết bị và không đăng nhập được cho tới khi được mở khóa. Việc khóa được ghi vào nhật ký của hộ."
        confirmLabel="Khóa tài khoản"
        destructive
        onConfirm={() => runLock(true)}
        pending={lockAccount.isPending}
      />
      <ConfirmDialog
        open={dialog === "unlock"}
        onOpenChange={(open) => setDialog(open ? "unlock" : null)}
        title={`Mở khóa tài khoản ${account.fullName}?`}
        description="Tài khoản đăng nhập lại được bằng mật khẩu hiện tại."
        confirmLabel="Mở khóa"
        onConfirm={() => runLock(false)}
        pending={unlockAccount.isPending}
      />
      <ResetPasswordDialog
        accountName={account.fullName}
        open={dialog === "reset-password"}
        onOpenChange={(open) => setDialog(open ? "reset-password" : null)}
        reset={(values) => resetPassword.mutateAsync(values)}
        pending={resetPassword.isPending}
        successMessage="Đã đặt lại mật khẩu. Hãy báo mật khẩu mới cho Chủ hộ."
      />
    </div>
  );
}

function InfoRow({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words">{children}</dd>
    </div>
  );
}
