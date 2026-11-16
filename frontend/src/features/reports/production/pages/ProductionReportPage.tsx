import { useState } from "react";
import { useSearchParams } from "react-router";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Input } from "@/components/ui/input";
import { CancelledSeasonsSection } from "@/features/reports/common/components/CancelledSeasonsSection";
import { ExportButtons } from "@/features/reports/common/components/ExportButtons";
import { ReportFilterBar } from "@/features/reports/common/components/ReportFilterBar";
import { PreviousComparisonList } from "@/features/reports/production/components/PreviousComparisonList";
import { ProductionSeasonList } from "@/features/reports/production/components/ProductionSeasonList";
import { ProductionYieldChart } from "@/features/reports/production/components/ProductionYieldChart";
import {
  applyProductionFilterParams,
  DEFAULT_DEVIATION_THRESHOLD,
  readProductionReportQuery,
} from "@/features/reports/production/filter";
import { useProductionReport } from "@/features/reports/production/queries";
import type { PreviousSeasonCompareBy } from "@/features/reports/production/types";
import { ACCESS } from "@/lib/auth/access";
import { formatArea, parseDecimalInput } from "@/lib/format";

function OverviewTile({ label, value, subtext }: { label: string; value: string | number; subtext?: string }) {
  return (
    <div className="flex flex-col gap-1 rounded-lg border bg-card p-3">
      <span className="text-sm text-muted-foreground">{label}</span>
      <span className="text-lg font-semibold tabular-nums">{value}</span>
      {subtext && <span className="text-xs text-muted-foreground tabular-nums">{subtext}</span>}
    </div>
  );
}

/**
 * `/reports/production`: báo cáo sản xuất (Chủ hộ, Thành viên PV; FR-RPT-PROD-01..05).
 */
export function ProductionReportPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readProductionReportQuery(searchParams);
  const report = useProductionReport(query);
  const currentThreshold = query.deviationThreshold ?? DEFAULT_DEVIATION_THRESHOLD;
  const [syncedThreshold, setSyncedThreshold] = useState(currentThreshold);
  const [thresholdInput, setThresholdInput] = useState(String(currentThreshold));

  if (syncedThreshold !== currentThreshold) {
    setSyncedThreshold(currentThreshold);
    setThresholdInput(String(currentThreshold));
  }

  function updateQuery(
    changes: Partial<{
      from: string | undefined;
      to: string | undefined;
      seasonId: string | undefined;
      plotId: string | undefined;
      cropId: string | undefined;
      deviationThreshold: number | undefined;
      onlyHighDeviation: boolean | undefined;
      compareBy: PreviousSeasonCompareBy | undefined;
    }>,
  ) {
    setSearchParams((params) => applyProductionFilterParams(params, changes));
  }

  function commitThreshold() {
    const parsed = parseDecimalInput(thresholdInput);
    if (parsed !== null && parsed > 0) {
      setThresholdInput(String(parsed));
      if (parsed !== currentThreshold) {
        updateQuery({ deviationThreshold: parsed });
      }
      return;
    }
    setThresholdInput(String(currentThreshold));
  }

  const extraActiveFilters = [
    ...(query.deviationThreshold !== undefined && query.deviationThreshold !== DEFAULT_DEVIATION_THRESHOLD
      ? [
          {
            key: "deviationThreshold",
            label: `Ngưỡng lệch: ≥ ${query.deviationThreshold}%`,
            onRemove: () => updateQuery({ deviationThreshold: DEFAULT_DEVIATION_THRESHOLD }),
          },
        ]
      : []),
    ...(query.onlyHighDeviation
      ? [
          {
            key: "onlyHighDeviation",
            label: "Chỉ mùa vụ lệch nhiều",
            onRemove: () => updateQuery({ onlyHighDeviation: false }),
          },
        ]
      : []),
  ];

  const extraFilters = (
    <>
      <label className="flex flex-col gap-1 text-sm">
        <span className="text-muted-foreground">Ngưỡng lệch (%)</span>
        <Input
          type="text"
          inputMode="decimal"
          className="min-h-11 w-full md:min-h-9 md:w-28"
          value={thresholdInput}
          onChange={(event) => setThresholdInput(event.target.value)}
          onBlur={commitThreshold}
          onKeyDown={(event) => {
            if (event.key === "Enter") {
              event.preventDefault();
              commitThreshold();
            }
          }}
        />
      </label>

      <label className="flex min-h-11 items-center gap-2 self-end rounded-md border px-3 text-sm md:min-h-9">
        <input
          type="checkbox"
          className="size-4 accent-primary"
          checked={Boolean(query.onlyHighDeviation)}
          onChange={(event) => updateQuery({ onlyHighDeviation: event.target.checked })}
        />
        <span>Chỉ mùa vụ lệch nhiều</span>
      </label>
    </>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title="Báo cáo sản xuất"
        backTo="/reports"
        action={
          <ExportButtons
            endpoint="/reports/production/export"
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
        extraFilters={extraFilters}
        extraActiveFilters={extraActiveFilters}
      />

      {report.isPending ? (
        <LoadingState variant="detail" />
      ) : report.isError ? (
        <ErrorAlert error={report.error} onRetry={() => void report.refetch()} />
      ) : (
        <div className="flex flex-col gap-4">
          <div className="grid grid-cols-2 gap-3 lg:grid-cols-4">
            <OverviewTile label="Đang canh tác" value={report.data.overview.inProgressCount} />
            <OverviewTile label="Đã chốt" value={report.data.overview.closedCount} />
            <OverviewTile label="Lên kế hoạch" value={report.data.overview.plannedCount} />
            <OverviewTile
              label="Tổng diện tích gieo trồng"
              value={formatArea(report.data.overview.totalCultivatedAreaM2)}
              subtext={formatArea(report.data.overview.totalCultivatedAreaM2, "ha")}
            />
          </div>

          <section className="flex flex-col gap-3 rounded-lg border bg-card p-4">
            <div>
              <h2 className="text-base font-semibold">Sản lượng và năng suất theo mùa vụ</h2>
              <p className="mt-0.5 text-sm text-muted-foreground">
                Mùa vụ đã chốt có độ lệch tuyệt đối từ {report.data.deviationThreshold}% trở lên được đánh dấu Lệch nhiều.
              </p>
            </div>

            {report.data.seasons.length === 0 ? (
              <EmptyState message="Không có mùa vụ nào khớp bộ lọc hiện tại." />
            ) : (
              <ProductionSeasonList
                items={report.data.seasons}
                includesFinancialColumns={report.data.includesFinancialColumns}
              />
            )}
          </section>

          <ProductionYieldChart seasons={report.data.seasons} />

          <PreviousComparisonList
            items={report.data.seasons}
            compareBy={query.compareBy ?? "SamePlot"}
            onCompareByChange={(compareBy) => updateQuery({ compareBy })}
            includesFinancialColumns={report.data.includesFinancialColumns}
          />

          <CancelledSeasonsSection
            items={report.data.cancelledSeasons}
            renderItems={(items) => (
              <ProductionSeasonList
                items={items}
                includesFinancialColumns={report.data.includesFinancialColumns}
              />
            )}
          />
        </div>
      )}
    </div>
  );
}
