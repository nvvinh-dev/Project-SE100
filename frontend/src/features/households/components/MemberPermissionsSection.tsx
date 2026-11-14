import { useState } from "react";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showSuccess } from "@/components/common/toast";
import { SwitchRow } from "@/features/households/components/SwitchRow";
import { useChangeMemberPermissions } from "@/features/households/queries";
import type { MemberResponse } from "@/features/households/types";
import { ApiError } from "@/lib/api-error";
import { extraPermissionLabels } from "@/lib/enum-labels";
import type { ExtraPermission } from "@/lib/enums";

type PermissionField = "canManageStock" | "canManageFinance" | "canCloseSeason";

// Phạm vi của từng quyền, theo bảng quyền bổ sung của yêu cầu phần mềm.
const permissions: readonly { permission: ExtraPermission; field: PermissionField; description: string }[] = [
  {
    permission: "ManageStock",
    field: "canManageStock",
    description: "Nhập kho, xuất kho, điều chỉnh tồn vật tư; xem giá trị kho, lịch sử kho, báo cáo vật tư. Áp dụng cả hộ.",
  },
  {
    permission: "ManageFinance",
    field: "canManageFinance",
    description: "Xem và ghi chi phí, giao dịch bán, thanh toán; xem báo cáo tài chính. Áp dụng cả hộ.",
  },
  {
    permission: "CloseSeason",
    field: "canCloseSeason",
    description: "Chốt các mùa vụ được phân công.",
  },
];

/**
 * Bật, tắt ba quyền bổ sung (FR-HH-07). Mỗi lần bật, tắt lưu ngay; quyền có hiệu lực từ thao tác kế tiếp của thành viên,
 * không cần đăng nhập lại. Nhân công không có quyền bổ sung (BR-USR-09).
 */
export function MemberPermissionsSection({ member, onReload }: { member: MemberResponse; onReload: () => void }) {
  const changePermissions = useChangeMemberPermissions(member.id);
  const [error, setError] = useState<unknown>(null);
  const isConflict = error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT";

  if (member.role !== "Member") {
    return <p className="text-sm text-muted-foreground">Nhân công không có quyền bổ sung.</p>;
  }

  function toggle(field: PermissionField, checked: boolean, label: string) {
    setError(null);
    changePermissions.mutate(
      {
        canManageStock: member.canManageStock,
        canManageFinance: member.canManageFinance,
        canCloseSeason: member.canCloseSeason,
        [field]: checked,
        version: member.version,
      },
      {
        onSuccess: () => showSuccess(`Đã ${checked ? "bật" : "tắt"} quyền ${label}.`),
        onError: setError,
      },
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <div className="divide-y rounded-lg border px-4">
        {permissions.map(({ permission, field, description }) => (
          <SwitchRow
            key={permission}
            id={`permission-${permission}`}
            label={extraPermissionLabels[permission]}
            description={description}
            checked={member[field]}
            disabled={changePermissions.isPending}
            onCheckedChange={(checked) => toggle(field, checked, extraPermissionLabels[permission])}
          />
        ))}
      </div>
      {error !== null && <ErrorAlert error={error} onRetry={isConflict ? onReload : undefined} retryLabel="Tải lại" />}
    </div>
  );
}
