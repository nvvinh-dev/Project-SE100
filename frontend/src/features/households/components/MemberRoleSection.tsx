import { useId, useState } from "react";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useChangeMemberRole } from "@/features/households/queries";
import { type MemberResponse, type MemberRole, memberRoles } from "@/features/households/types";
import { ApiError } from "@/lib/api-error";
import { userRoleLabels } from "@/lib/enum-labels";

/**
 * Đổi vai trò qua lại giữa Thành viên hộ và Nhân công (FR-HH-04). Hỏi trước vì thành viên bị đăng xuất trên mọi thiết bị
 * (BR-USR-04); sang Nhân công thì ba quyền bổ sung bị gỡ (BR-USR-09).
 */
export function MemberRoleSection({ member, onReload }: { member: MemberResponse; onReload: () => void }) {
  const selectId = useId();
  const changeRole = useChangeMemberRole(member.id);
  const currentRole = member.role === "Worker" ? "Worker" : "Member";
  const [role, setRole] = useState<MemberRole>(currentRole);
  const [confirming, setConfirming] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const isConflict = error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT";
  const hasPermissions = member.canManageStock || member.canManageFinance || member.canCloseSeason;

  function confirm() {
    setError(null);
    changeRole.mutate(
      { role, version: member.version },
      {
        onSuccess: (saved) => {
          setConfirming(false);
          showSuccess(`Đã đổi vai trò thành ${userRoleLabels[saved.role]}.`);
        },
        onError: (failure) => {
          setConfirming(false);
          setError(failure);
        },
      },
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <label htmlFor={selectId} className="text-sm font-medium">
        Vai trò
      </label>
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
        <NativeSelect
          id={selectId}
          className="w-full sm:w-56"
          value={role}
          onChange={(event) => setRole(event.target.value === "Worker" ? "Worker" : "Member")}
        >
          {memberRoles.map((value) => (
            <NativeSelectOption key={value} value={value}>
              {userRoleLabels[value]}
            </NativeSelectOption>
          ))}
        </NativeSelect>
        <Button type="button" variant="outline" disabled={role === currentRole} onClick={() => setConfirming(true)}>
          Đổi vai trò
        </Button>
      </div>

      {error !== null && <ErrorAlert error={error} onRetry={isConflict ? onReload : undefined} retryLabel="Tải lại" />}

      <ConfirmDialog
        open={confirming}
        onOpenChange={setConfirming}
        title={`Đổi vai trò thành ${userRoleLabels[role]}?`}
        description={
          role === "Worker" && hasPermissions
            ? "Thành viên sẽ bị đăng xuất trên mọi thiết bị để nhận quyền mới. Ba quyền bổ sung đang bật sẽ bị gỡ."
            : "Thành viên sẽ bị đăng xuất trên mọi thiết bị để nhận quyền mới."
        }
        confirmLabel="Đổi vai trò"
        onConfirm={confirm}
        pending={changeRole.isPending}
      />
    </div>
  );
}
