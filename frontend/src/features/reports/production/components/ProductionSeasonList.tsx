import { MoneyText } from "@/components/common/MoneyText";
import { QuantityText } from "@/components/common/QuantityText";
import { Badge } from "@/components/ui/badge";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ReportSeasonStatusBadge } from "@/features/reports/common/components/ReportSeasonStatusBadge";
import type { ProductionSeasonItem } from "@/features/reports/production/types";
import { unitLabels } from "@/lib/enum-labels";
import { formatArea } from "@/lib/format";
import { cn } from "@/lib/utils";

const percentFormat = new Intl.NumberFormat("vi-VN", {
  minimumFractionDigits: 1,
  maximumFractionDigits: 1,
});
const yieldFormat = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 2 });

function formatDeviation(value: number) {
  const sign = value > 0 ? "+" : "";
  return `${sign}${percentFormat.format(value)}%`;
}

function seasonTitle(item: ProductionSeasonItem) {
  return `${item.season.cropName} · ${item.season.plotCode}`;
}

/**
 * Danh sách sản lượng, độ lệch và năng suất theo mùa vụ (FR-RPT-PROD-02, FR-RPT-PROD-03):
 * điện thoại hiện dạng thẻ, từ `md` hiện bảng. Cột tài chính chỉ hiện khi backend trả về (BR-RPT-04).
 */
export function ProductionSeasonList({
  items,
  includesFinancialColumns,
}: {
  items: readonly ProductionSeasonItem[];
  includesFinancialColumns: boolean;
}) {
  return (
    <>
      <ul className="flex flex-col gap-3 md:hidden">
        {items.map((item) => (
          <li key={item.season.seasonId} className="flex flex-col gap-2 rounded-lg border bg-card p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium">{seasonTitle(item)}</span>
              <div className="flex flex-wrap items-center gap-1.5">
                <ReportSeasonStatusBadge
                  status={item.season.status}
                  isProvisional={item.season.isProvisional}
                />
                {item.isHighDeviation && (
                  <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Lệch nhiều</Badge>
                )}
              </div>
            </div>

            <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
              <dt className="text-muted-foreground">Diện tích</dt>
              <dd className="text-right tabular-nums">{formatArea(item.season.areaM2)}</dd>
              <dt className="text-muted-foreground">Sản lượng / Mục tiêu</dt>
              <dd className="text-right tabular-nums">
                <QuantityText value={item.actualYield} unit={item.season.yieldUnit} /> /{" "}
                <QuantityText value={item.targetYield} unit={item.season.yieldUnit} />
              </dd>
              <dt className="text-muted-foreground">Độ lệch</dt>
              <dd
                className={cn(
                  "text-right font-medium tabular-nums",
                  item.isHighDeviation && "text-amber-700",
                )}
              >
                {formatDeviation(item.yieldDeviation)}
              </dd>
              <dt className="text-muted-foreground">Năng suất</dt>
              <dd className="text-right tabular-nums">
                {yieldFormat.format(item.yieldPerHectare)} {unitLabels[item.season.yieldUnit]}/ha
              </dd>
              {includesFinancialColumns && (
                <>
                  <dt className="text-muted-foreground">Chi phí vật tư</dt>
                  <dd className="text-right">
                    <MoneyText amount={item.supplyCost ?? 0} />
                  </dd>
                  <dt className="text-muted-foreground">Doanh thu</dt>
                  <dd className="text-right">
                    <MoneyText amount={item.revenue ?? 0} />
                  </dd>
                  <dt className="font-medium">Lợi nhuận</dt>
                  <dd className="text-right">
                    <MoneyText
                      amount={item.profit ?? 0}
                      className={cn("font-semibold", (item.profit ?? 0) < 0 && "text-destructive")}
                    />
                  </dd>
                </>
              )}
            </dl>
          </li>
        ))}
      </ul>

      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Cây · Thửa</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="text-right">Diện tích</TableHead>
              <TableHead className="text-right">Thực tế / Mục tiêu</TableHead>
              <TableHead className="text-right">Độ lệch</TableHead>
              <TableHead className="text-right">Năng suất / ha</TableHead>
              {includesFinancialColumns && (
                <>
                  <TableHead className="text-right whitespace-normal">Chi phí vật tư</TableHead>
                  <TableHead className="text-right">Doanh thu</TableHead>
                  <TableHead className="text-right">Lợi nhuận</TableHead>
                </>
              )}
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={item.season.seasonId}>
                <TableCell className="font-medium">
                  <div className="flex flex-wrap items-center gap-2">
                    <span>{seasonTitle(item)}</span>
                    {item.isHighDeviation && (
                      <Badge className="bg-amber-100 text-amber-800 hover:bg-amber-100">Lệch nhiều</Badge>
                    )}
                  </div>
                </TableCell>
                <TableCell>
                  <ReportSeasonStatusBadge
                    status={item.season.status}
                    isProvisional={item.season.isProvisional}
                  />
                </TableCell>
                <TableCell className="text-right tabular-nums">{formatArea(item.season.areaM2)}</TableCell>
                <TableCell className="text-right tabular-nums">
                  <QuantityText value={item.actualYield} unit={item.season.yieldUnit} /> /{" "}
                  <QuantityText value={item.targetYield} unit={item.season.yieldUnit} />
                </TableCell>
                <TableCell
                  className={cn(
                    "text-right font-medium tabular-nums",
                    item.isHighDeviation && "text-amber-700",
                  )}
                >
                  {formatDeviation(item.yieldDeviation)}
                </TableCell>
                <TableCell className="text-right tabular-nums">
                  {yieldFormat.format(item.yieldPerHectare)} {unitLabels[item.season.yieldUnit]}/ha
                </TableCell>
                {includesFinancialColumns && (
                  <>
                    <TableCell className="text-right">
                      <MoneyText amount={item.supplyCost ?? 0} />
                    </TableCell>
                    <TableCell className="text-right">
                      <MoneyText amount={item.revenue ?? 0} />
                    </TableCell>
                    <TableCell className="text-right">
                      <MoneyText
                        amount={item.profit ?? 0}
                        className={cn("font-semibold", (item.profit ?? 0) < 0 && "text-destructive")}
                      />
                    </TableCell>
                  </>
                )}
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </>
  );
}
