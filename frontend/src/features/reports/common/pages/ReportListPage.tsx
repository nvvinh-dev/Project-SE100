import {
  ChevronRightIcon,
  ClipboardCheckIcon,
  PackageIcon,
  SproutIcon,
  WalletIcon,
} from "lucide-react";
import { Link } from "react-router";
import { EmptyState } from "@/components/common/EmptyState";
import { PageHeader } from "@/components/common/PageHeader";
import { ACCESS, visibleTo } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { reportTypeLabels } from "@/lib/enum-labels";

const reportCards = [
  {
    type: "Production" as const,
    to: "/reports/production",
    description: "Sản lượng thực tế, độ lệch mục tiêu, năng suất và so với mùa trước.",
    icon: SproutIcon,
    access: ACCESS.household,
  },
  {
    type: "FarmTask" as const,
    to: "/reports/farm-tasks",
    description: "Thống kê công việc đã lập, hoàn thành, trễ hạn để điều phối công việc.",
    icon: ClipboardCheckIcon,
    access: ACCESS.household,
  },
  {
    type: "Supply" as const,
    to: "/reports/supplies",
    description: "Xuất nhập tồn vật tư trong kỳ và đối chiếu xuất kho với công việc.",
    icon: PackageIcon,
    access: ACCESS.stockManager,
  },
  {
    type: "Finance" as const,
    to: "/reports/finance",
    description: "Chi phí, doanh thu, lợi nhuận theo mùa vụ, cơ cấu chi phí và dòng tiền.",
    icon: WalletIcon,
    access: ACCESS.financeManager,
  },
];

/** `/reports`: trang danh sách các báo cáo, tự ẩn báo cáo không thuộc quyền xem của người dùng (FR-RPT-02). */
export function ReportListPage() {
  const { data: user } = useCurrentUser();
  if (!user) {
    return null;
  }

  const allowedReports = visibleTo(reportCards, user);

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Báo cáo"
        description="Tổng hợp số liệu sản xuất, công việc, kho vật tư và tài chính theo bộ lọc thời gian."
      />

      {allowedReports.length === 0 ? (
        <EmptyState message="Tài khoản hiện không có quyền xem báo cáo nào." />
      ) : (
        <ul className="grid grid-cols-1 gap-3 md:grid-cols-2">
          {allowedReports.map((report) => (
            <li key={report.to}>
              <Link
                to={report.to}
                className="flex min-h-16 items-center gap-3 rounded-lg border bg-card p-4 transition-colors hover:bg-muted/50"
              >
                <div className="flex size-10 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-primary">
                  <report.icon className="size-5" aria-hidden />
                </div>
                <div className="min-w-0 flex-1">
                  <h2 className="font-semibold">{reportTypeLabels[report.type]}</h2>
                  <p className="mt-0.5 text-sm text-muted-foreground">{report.description}</p>
                </div>
                <ChevronRightIcon className="size-5 shrink-0 text-muted-foreground" aria-hidden />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
