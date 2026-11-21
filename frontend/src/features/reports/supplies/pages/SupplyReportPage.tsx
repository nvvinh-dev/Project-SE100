import { useSearchParams } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { ExportButtons } from "@/features/reports/common/components/ExportButtons";
import { ReportFilterBar } from "@/features/reports/common/components/ReportFilterBar";
import { ReportSection } from "@/features/reports/finance/components/ReportSection";
import { LowStockSection } from "@/features/reports/supplies/components/LowStockSection";
import { ManualOutList, ReconciliationList } from "@/features/reports/supplies/components/StockOutReconciliation";
import { SupplyStockGroups } from "@/features/reports/supplies/components/SupplyStockGroups";
import { applySupplyReportParams, readSupplyReportQuery } from "@/features/reports/supplies/filter";
import { useSupplyReport } from "@/features/reports/supplies/queries";
import type { SupplyReportQuery, SupplyReportResponse } from "@/features/reports/supplies/types";
import { ACCESS } from "@/lib/auth/access";
import { supplyGroupLabels } from "@/lib/enum-labels";
import { type SupplyGroup, supplyGroups } from "@/lib/enums";

const DATE_FILTER_ONLY = ["dateRange"] as const;

/**
 * `/reports/supplies`: báo cáo vật tư (Chủ hộ, TV+Kho; FR-RPT-SUP-01, FR-RPT-SUP-02, FR-RPT-SUP-03).
 * Bộ lọc khoảng ngày và nhóm vật tư lưu trên đường dẫn.
 */
export function SupplyReportPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readSupplyReportQuery(searchParams);
  const report = useSupplyReport(query);

  function changeQuery(changes: Partial<SupplyReportQuery>) {
    setSearchParams((params) => applySupplyReportParams(params, changes));
  }

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Báo cáo vật tư"
        backTo="/reports"
        action={
          <ExportButtons
            endpoint="/reports/supplies/export"
            filter={{ ...query }}
            access={ACCESS.stockManager}
            disabled={report.isPending || report.isError}
          />
        }
      />

      <ReportFilterBar
        filter={{ from: query.from, to: query.to }}
        onChange={(changes) => changeQuery(pickDates(changes))}
        onReset={() => setSearchParams(new URLSearchParams())}
        fields={DATE_FILTER_ONLY}
        extraFilters={
          <div className="flex flex-col gap-1.5 md:w-auto">
            <label htmlFor="report-filter-group" className="text-sm font-medium md:sr-only">
              Nhóm vật tư
            </label>
            <NativeSelect
              id="report-filter-group"
              className="min-h-11 w-full md:min-h-9 md:w-auto"
              value={query.group ?? ""}
              onChange={(event) => changeQuery({ group: (event.target.value || undefined) as SupplyGroup | undefined })}
            >
              <NativeSelectOption value="">Nhóm vật tư: tất cả</NativeSelectOption>
              {supplyGroups.map((group) => (
                <NativeSelectOption key={group} value={group}>
                  {supplyGroupLabels[group]}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>
        }
        extraActiveFilters={
          query.group
            ? [
                {
                  key: "group",
                  label: `Nhóm: ${supplyGroupLabels[query.group]}`,
                  onRemove: () => changeQuery({ group: undefined }),
                },
              ]
            : []
        }
      />

      {report.isPending ? (
        <LoadingState variant="detail" />
      ) : report.isError ? (
        <ErrorAlert error={report.error} onRetry={() => void report.refetch()} />
      ) : (
        <SupplyReportContent report={report.data} />
      )}
    </div>
  );
}

// ReportFilterBar chỉ đổi một ngày mỗi lần; giữ nguyên ngày còn lại thay vì xóa nó.
function pickDates(changes: Partial<{ from: string; to: string }>): Partial<SupplyReportQuery> {
  const picked: Partial<SupplyReportQuery> = {};
  if ("from" in changes) picked.from = changes.from;
  if ("to" in changes) picked.to = changes.to;
  return picked;
}

function SupplyReportContent({ report }: { report: SupplyReportResponse }) {
  return (
    <div className="flex flex-col gap-4">
      <LowStockSection items={report.lowStock} />

      <ReportSection
        title="Nhập, xuất, tồn theo nhóm vật tư"
        description="Lượng theo đơn vị của từng vật tư, theo ngày giao dịch trong kỳ. Dòng tổng nhóm chỉ cộng giá trị."
      >
        <div className="flex flex-col gap-1 rounded-lg border p-3 sm:max-w-xs">
          <span className="text-sm text-muted-foreground">Tổng giá trị tồn hiện tại</span>
          <MoneyText amount={report.totalStockValue} className="text-lg font-semibold" />
        </div>
        <SupplyStockGroups groups={report.groups} />
        <p className="text-xs text-muted-foreground">Giá trị tồn tính theo giá vốn bình quân hiện tại.</p>
      </ReportSection>

      <ReportSection
        title="Đối chiếu xuất kho"
        description="Tổng xuất kho trong kỳ so với lượng vật tư ghi trong công việc. Chênh lệch là phần xuất không ghi vào công việc nào."
      >
        <ReconciliationList items={report.reconciliation} />
        <h3 className="pt-2 text-sm font-semibold">Các lần xuất thủ công</h3>
        <ManualOutList items={report.manualOuts} />
      </ReportSection>
    </div>
  );
}
