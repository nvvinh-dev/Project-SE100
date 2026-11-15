import { StatusBadge } from "@/components/common/StatusBadge";
import type { SeasonStatus } from "@/lib/enums";

interface ReportSeasonStatusBadgeProps {
  status: SeasonStatus;
  isProvisional: boolean;
}

/**
 * Nhãn trạng thái mùa vụ trong báo cáo kèm chữ "(tạm tính)" khi mùa vụ chưa chốt (BR-RPT-03).
 */
export function ReportSeasonStatusBadge({ status, isProvisional }: ReportSeasonStatusBadgeProps) {
  return (
    <span className="inline-flex flex-wrap items-center gap-1.5">
      <StatusBadge kind="SeasonStatus" value={status} />
      {isProvisional && <span className="text-xs font-medium text-muted-foreground">(tạm tính)</span>}
    </span>
  );
}
