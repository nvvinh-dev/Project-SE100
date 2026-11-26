import { useSearchParams } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { CancelledSeasonsSection } from "@/features/reports/common/components/CancelledSeasonsSection";
import { ExportButtons } from "@/features/reports/common/components/ExportButtons";
import { ReportFilterBar } from "@/features/reports/common/components/ReportFilterBar";
import { applyFilterParams, readReportFilter } from "@/features/reports/common/filter";
import type { ReportFilter } from "@/features/reports/common/types";
import { AmountList, type AmountRow } from "@/features/reports/finance/components/AmountList";
import { CostStructureChart } from "@/features/reports/finance/components/CostStructureChart";
import { FinanceSeasonList } from "@/features/reports/finance/components/FinanceSeasonList";
import { MonthlyChart } from "@/features/reports/finance/components/MonthlyChart";
import { MonthlyTable } from "@/features/reports/finance/components/MonthlyTable";
import { ReportSection } from "@/features/reports/finance/components/ReportSection";
import { costRows } from "@/features/reports/finance/cost-rows";
import { useFinanceReport } from "@/features/reports/finance/queries";
import type { FinanceReportResponse } from "@/features/reports/finance/types";
import { ACCESS } from "@/lib/auth/access";
import { salesChannelLabels } from "@/lib/enum-labels";
import { cn } from "@/lib/utils";

function SummaryTile({ label, amount, note }: { label: string; amount: number; note?: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border bg-card p-3">
      <span className="text-sm text-muted-foreground">{label}</span>
      <MoneyText amount={amount} className={cn("text-lg font-semibold", amount < 0 && "text-destructive")} />
      {note && <span className="text-xs text-muted-foreground">{note}</span>}
    </div>
  );
}

/**
 * `/reports/finance`: báo cáo chi phí - doanh thu, dòng tiền (Chủ hộ, TV+TC; FR-RPT-FIN-01, 03 đến 06).
 * Bộ lọc lưu trên đường dẫn; dòng tiền chỉ lọc theo thời gian (BR-RPT-07).
 */
export function FinanceReportPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const filter = readReportFilter(searchParams);
  const report = useFinanceReport(filter);
  const hasScopeFilter = Boolean(filter.seasonId || filter.plotId || filter.cropId);

  function changeFilter(changes: Partial<ReportFilter>) {
    setSearchParams((params) => applyFilterParams(params, changes));
  }

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Báo cáo chi phí - doanh thu"
        backTo="/reports"
        action={
          <ExportButtons
            endpoint="/reports/finance/export"
            filter={{ ...filter }}
            access={ACCESS.financeManager}
            disabled={report.isPending || report.isError}
          />
        }
      />

      <ReportFilterBar
        filter={filter}
        onChange={changeFilter}
        onReset={() => setSearchParams(new URLSearchParams())}
        seasonPermission="ManageFinance"
      />

      {report.isPending ? (
        <LoadingState variant="detail" />
      ) : report.isError ? (
        <ErrorAlert error={report.error} onRetry={() => void report.refetch()} />
      ) : (
        <FinanceReportContent report={report.data} hasScopeFilter={hasScopeFilter} />
      )}
    </div>
  );
}

function FinanceReportContent({ report, hasScopeFilter }: { report: FinanceReportResponse; hasScopeFilter: boolean }) {
  const costs = costRows(report);
  const revenues: AmountRow[] = report.revenueByChannel.map((item) => ({
    key: item.channel ?? "none",
    label: item.channel ? salesChannelLabels[item.channel] : "Không ghi kênh",
    amount: item.amount,
  }));

  return (
    <div className="flex flex-col gap-4">
      <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
        {/* Tiền mua vật tư chỉ thành chi phí khi xuất cho mùa vụ; không ghi rõ thì người dùng tưởng báo cáo thiếu tiền mua. */}
        <SummaryTile
          label="Tổng chi phí"
          amount={report.totalCost}
          note="Vật tư tính khi xuất kho; tiền mua xem ở Dòng tiền"
        />
        <SummaryTile label="Tổng doanh thu" amount={report.totalRevenue} />
        <SummaryTile label="Lợi nhuận đã chốt" amount={report.closedProfit} note="Mùa vụ đã chốt" />
        <SummaryTile label="Lợi nhuận tạm tính" amount={report.provisionalProfit} note="Mùa vụ chưa chốt" />
      </div>

      <div className="grid gap-4 lg:grid-cols-2">
        <ReportSection
          title="Chi phí theo nhóm"
          description="Theo ngày phát sinh trong kỳ. Chi phí vật tư là giá trị xuất kho cho mùa vụ."
        >
          <CostStructureChart rows={costs} />
          <AmountList
            rows={[...costs, { key: "total", label: "Tổng chi phí", amount: report.totalCost, total: true }]}
            emptyMessage="Chưa có chi phí trong kỳ."
          />
          {hasScopeFilter && (
            <p className="text-xs text-muted-foreground">
              Đang lọc theo mùa vụ, thửa hoặc cây trồng nên không có chi phí chung của hộ.
            </p>
          )}
        </ReportSection>

        <ReportSection title="Doanh thu theo kênh bán" description="Theo ngày bán trong kỳ.">
          <AmountList
            rows={[...revenues, { key: "total", label: "Tổng doanh thu", amount: report.totalRevenue, total: true }]}
            emptyMessage="Chưa có doanh thu trong kỳ."
          />
        </ReportSection>
      </div>

      <ReportSection
        title="Lợi nhuận theo mùa vụ"
        description="Tính cho cả mùa vụ, không cắt theo kỳ lọc. Mùa vụ chưa chốt là số tạm tính."
      >
        {report.seasons.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có mùa vụ nào khớp bộ lọc.</p>
        ) : (
          <FinanceSeasonList items={report.seasons} />
        )}
      </ReportSection>

      <CancelledSeasonsSection
        items={report.cancelledSeasons}
        description="Mùa vụ đã hủy tách riêng, không nằm trong hai dòng lợi nhuận đã chốt và tạm tính."
        renderItems={(items) => <FinanceSeasonList items={items} />}
      />

      <ReportSection title="Dòng tiền" description="Giả định: vật tư nhập kho được trả tiền ngay tại ngày nhập.">
        <AmountList
          rows={[
            { key: "stock-in", label: "Tiền chi nhập kho", amount: report.cashFlow.stockInAmount },
            { key: "expense", label: "Tiền chi phí ghi tay", amount: report.cashFlow.expenseAmount },
            { key: "cash-out", label: "Tổng tiền chi", amount: report.cashFlow.cashOut, total: true },
            { key: "cash-in", label: "Tiền thu", amount: report.cashFlow.cashIn, total: true },
            { key: "unpaid", label: "Doanh thu chưa thu (đến ngày cuối kỳ)", amount: report.cashFlow.unpaidRevenue },
          ]}
          emptyMessage=""
        />
        {hasScopeFilter && <p className="text-xs text-muted-foreground">Dòng tiền chỉ xem theo thời gian.</p>}
      </ReportSection>

      <ReportSection
        title="Doanh thu và chi phí theo tháng"
        description="Chi phí là giá trị xuất kho và chi phí ghi tay, không phải tiền chi của dòng tiền."
      >
        {report.monthly.length === 0 ? (
          <p className="text-sm text-muted-foreground">Chưa có số liệu trong kỳ.</p>
        ) : (
          <>
            <MonthlyChart items={report.monthly} />
            <MonthlyTable items={report.monthly} />
          </>
        )}
      </ReportSection>
    </div>
  );
}
