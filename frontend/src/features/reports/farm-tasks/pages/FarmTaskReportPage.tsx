import { useSearchParams } from "react-router";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ExportButtons } from "@/features/reports/common/components/ExportButtons";
import { ReportFilterBar } from "@/features/reports/common/components/ReportFilterBar";
import { applyFarmTaskFilterParams, readFarmTaskReportQuery } from "@/features/reports/farm-tasks/filter";
import { useFarmTaskReport } from "@/features/reports/farm-tasks/queries";
import type { FarmTaskReportGroupBy, FarmTaskReportRow } from "@/features/reports/farm-tasks/types";
import { ACCESS } from "@/lib/auth/access";
import { cn } from "@/lib/utils";

function SummaryTile({ label, value, highlightDanger }: { label: string; value: number; highlightDanger?: boolean }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border bg-card p-3">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className={cn("text-lg font-semibold tabular-nums", highlightDanger && value > 0 && "text-red-700")}>
        {value}
      </span>
    </div>
  );
}

function FarmTaskReportRows({
  rows,
  groupBy,
}: {
  rows: readonly FarmTaskReportRow[];
  groupBy: FarmTaskReportGroupBy;
}) {
  const groupHeader = groupBy === "Assignee" ? "Người phụ trách" : "Tháng";

  return (
    <>
      <ul className="flex flex-col gap-3 md:hidden">
        {rows.map((row) => (
          <li key={row.key} className="flex flex-col gap-2 rounded-lg border bg-card p-3">
            <span className="font-medium">{row.label}</span>
            <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
              <dt className="text-muted-foreground">Đã lập</dt>
              <dd className="text-right font-medium tabular-nums">{row.plannedCount}</dd>
              <dt className="text-muted-foreground">Hoàn thành</dt>
              <dd className="text-right tabular-nums">{row.completedCount}</dd>
              <dt className="text-muted-foreground">Trễ hạn</dt>
              <dd className={cn("text-right tabular-nums", row.overdueCount > 0 && "font-medium text-red-700")}>
                {row.overdueCount}
              </dd>
              <dt className="text-muted-foreground">Chưa thực hiện</dt>
              <dd className="text-right tabular-nums">{row.pendingCount}</dd>
            </dl>
          </li>
        ))}
      </ul>

      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>{groupHeader}</TableHead>
              <TableHead className="text-right">Đã lập</TableHead>
              <TableHead className="text-right">Hoàn thành</TableHead>
              <TableHead className="text-right">Trễ hạn</TableHead>
              <TableHead className="text-right">Chưa thực hiện</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {rows.map((row) => (
              <TableRow key={row.key}>
                <TableCell className="font-medium">{row.label}</TableCell>
                <TableCell className="text-right font-medium tabular-nums">{row.plannedCount}</TableCell>
                <TableCell className="text-right tabular-nums">{row.completedCount}</TableCell>
                <TableCell
                  className={cn("text-right tabular-nums", row.overdueCount > 0 && "font-medium text-red-700")}
                >
                  {row.overdueCount}
                </TableCell>
                <TableCell className="text-right tabular-nums">{row.pendingCount}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </>
  );
}

/**
 * `/reports/farm-tasks`: báo cáo công việc điều phối (Chủ hộ, Thành viên PV; FR-RPT-TASK-01, FR-RPT-TASK-02).
 */
export function FarmTaskReportPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readFarmTaskReportQuery(searchParams);
  const report = useFarmTaskReport(query);

  function updateQuery(
    changes: Partial<{
      from: string | undefined;
      to: string | undefined;
      seasonId: string | undefined;
      plotId: string | undefined;
      cropId: string | undefined;
      groupBy: FarmTaskReportGroupBy | undefined;
    }>,
  ) {
    setSearchParams((params) => applyFarmTaskFilterParams(params, changes));
  }

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Báo cáo công việc"
        description="Báo cáo dùng để điều phối công việc."
        backTo="/reports"
        action={
          <ExportButtons
            endpoint="/reports/farm-tasks/export"
            filter={{ ...query }}
            access={ACCESS.household}
            disabled={report.isPending || report.isError}
          />
        }
      />

      <ReportFilterBar
        filter={query}
        onChange={updateQuery}
        onReset={() => setSearchParams(new URLSearchParams())}
      />

      <div className="flex flex-wrap items-center justify-between gap-3">
        <span className="text-sm text-muted-foreground">
          Thời gian lọc theo ngày dự kiến thực hiện công việc.
        </span>
        <div className="inline-flex rounded-lg border bg-muted/40 p-1">
          <button
            type="button"
            onClick={() => updateQuery({ groupBy: "Time" })}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              query.groupBy === "Time" ? "bg-background shadow-sm" : "text-muted-foreground hover:text-foreground",
            )}
          >
            Theo thời gian
          </button>
          <button
            type="button"
            onClick={() => updateQuery({ groupBy: "Assignee" })}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              query.groupBy === "Assignee"
                ? "bg-background shadow-sm"
                : "text-muted-foreground hover:text-foreground",
            )}
          >
            Theo người phụ trách
          </button>
        </div>
      </div>

      {report.isPending ? (
        <LoadingState variant="detail" />
      ) : report.isError ? (
        <ErrorAlert error={report.error} onRetry={() => void report.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <SummaryTile label="Đã lập" value={report.data.overview.plannedCount} />
            <SummaryTile label="Hoàn thành" value={report.data.overview.completedCount} />
            <SummaryTile label="Trễ hạn" value={report.data.overview.overdueCount} highlightDanger />
            <SummaryTile label="Chưa thực hiện" value={report.data.overview.pendingCount} />
          </div>

          {report.data.overview.cancelledCount > 0 && (
            <p className="text-xs text-muted-foreground">
              Có {report.data.overview.cancelledCount} công việc đã hủy (tách riêng, không tính vào số đã lập).
            </p>
          )}

          <section className="flex flex-col gap-3 rounded-lg border bg-card p-4">
            <h2 className="text-base font-semibold">
              {report.data.groupBy === "Assignee"
                ? "Thống kê theo người phụ trách"
                : "Thống kê theo thời gian"}
            </h2>
            {report.data.rows.length === 0 ? (
              <EmptyState message="Không có công việc nào khớp bộ lọc hiện tại." />
            ) : (
              <FarmTaskReportRows rows={report.data.rows} groupBy={report.data.groupBy} />
            )}
          </section>
        </div>
      )}
    </div>
  );
}
