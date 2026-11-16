import type { PreviousSeasonCompareBy, ProductionSeasonItem } from "@/features/reports/production/types";
import { unitLabels } from "@/lib/enum-labels";
import { formatMoney } from "@/lib/format";
import { cn } from "@/lib/utils";

const yieldDeltaFormat = new Intl.NumberFormat("vi-VN", {
  maximumFractionDigits: 2,
});

function formatSignedMoney(amount: number) {
  if (amount > 0) return `+${formatMoney(amount)}`;
  return formatMoney(amount);
}

function formatSignedYield(delta: number, unitLabel: string) {
  const sign = delta > 0 ? "+" : "";
  return `${sign}${yieldDeltaFormat.format(delta)} ${unitLabel}/ha`;
}

/**
 * Khối "So với mùa trước" trên báo cáo sản xuất (FR-RPT-PROD-04, BR-RPT-11):
 * cho phép chuyển giữa "Cùng thửa" và "Cùng cây"; hiện chênh lệch năng suất và chi phí vật tư / doanh thu.
 */
export function PreviousComparisonList({
  items,
  compareBy,
  onCompareByChange,
  includesFinancialColumns,
}: {
  items: readonly ProductionSeasonItem[];
  compareBy: PreviousSeasonCompareBy;
  onCompareByChange: (value: PreviousSeasonCompareBy) => void;
  includesFinancialColumns: boolean;
}) {
  if (items.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3 rounded-lg border bg-card p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-base font-semibold">So với mùa trước</h2>
          <p className="mt-0.5 text-sm text-muted-foreground">
            Chọn mùa vụ đã chốt gần nhất trước ngày bắt đầu của từng mùa vụ (bỏ qua mùa đã hủy).
          </p>
        </div>

        <div className="inline-flex rounded-lg border bg-muted/40 p-1">
          <button
            type="button"
            onClick={() => onCompareByChange("SamePlot")}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              compareBy === "SamePlot" ? "bg-background shadow-sm" : "text-muted-foreground hover:text-foreground",
            )}
          >
            Cùng thửa
          </button>
          <button
            type="button"
            onClick={() => onCompareByChange("SameCrop")}
            className={cn(
              "rounded-md px-3 py-1.5 text-sm font-medium transition-colors",
              compareBy === "SameCrop" ? "bg-background shadow-sm" : "text-muted-foreground hover:text-foreground",
            )}
          >
            Cùng cây
          </button>
        </div>
      </div>

      <ul className="flex flex-col gap-3">
        {items.map((item) => {
          const comp = item.previousComparison;
          const unitLabel = unitLabels[item.season.yieldUnit];
          return (
            <li key={item.season.seasonId} className="flex flex-col gap-2 rounded-lg border p-3">
              <div className="flex flex-wrap items-baseline justify-between gap-2">
                <span className="font-medium">
                  {item.season.cropName} · {item.season.plotCode}
                </span>
                <span className="text-xs text-muted-foreground">
                  {comp?.previousSeasonLabel
                    ? `Mùa trước: ${comp.previousSeasonLabel}`
                    : (comp?.message ?? "Chưa có mùa trước để so sánh")}
                </span>
              </div>

              {comp?.previousSeasonId ? (
                <dl className="grid grid-cols-1 gap-2 text-sm md:grid-cols-3">
                  <div className="rounded-md bg-muted/30 p-2">
                    <dt className="text-xs text-muted-foreground">Chênh lệch năng suất</dt>
                    <dd className="mt-0.5 font-medium tabular-nums">
                      {comp.sameYieldUnit && comp.yieldPerHectareDelta !== null
                        ? formatSignedYield(comp.yieldPerHectareDelta, unitLabel)
                        : (comp.message ?? "Khác đơn vị, không so sánh")}
                    </dd>
                  </div>

                  {includesFinancialColumns && (
                    <>
                      <div className="rounded-md bg-muted/30 p-2">
                        <dt className="text-xs text-muted-foreground">Chênh lệch chi phí vật tư</dt>
                        <dd className="mt-0.5 font-medium tabular-nums">
                          {comp.supplyCostDelta !== null ? formatSignedMoney(comp.supplyCostDelta) : "—"}
                        </dd>
                        {comp.supplyCostPerM2Delta !== null && (
                          <dd className="text-xs text-muted-foreground tabular-nums">
                            Trên m²: {formatSignedMoney(comp.supplyCostPerM2Delta)}/m²
                          </dd>
                        )}
                      </div>

                      <div className="rounded-md bg-muted/30 p-2">
                        <dt className="text-xs text-muted-foreground">Chênh lệch doanh thu</dt>
                        <dd className="mt-0.5 font-medium tabular-nums">
                          {comp.revenueDelta !== null ? formatSignedMoney(comp.revenueDelta) : "—"}
                        </dd>
                        {comp.revenuePerM2Delta !== null && (
                          <dd className="text-xs text-muted-foreground tabular-nums">
                            Trên m²: {formatSignedMoney(comp.revenuePerM2Delta)}/m²
                          </dd>
                        )}
                      </div>
                    </>
                  )}
                </dl>
              ) : null}
            </li>
          );
        })}
      </ul>
    </section>
  );
}
