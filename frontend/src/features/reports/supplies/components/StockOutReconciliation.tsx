import { DateText } from "@/components/common/DateText";
import { QuantityText } from "@/components/common/QuantityText";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import type { ReportSeasonSummary } from "@/features/reports/common/types";
import type { SupplyManualOutItem, SupplyReconciliationItem } from "@/features/reports/supplies/types";
import { formatDate } from "@/lib/format";
import { cn } from "@/lib/utils";

function seasonLabel(season: ReportSeasonSummary): string {
  return `${season.cropName} · ${season.plotCode} · ${formatDate(season.startDate)}`;
}

function Difference({ item }: { item: SupplyReconciliationItem }) {
  const differs = item.differenceQuantity !== 0;
  return (
    <QuantityText
      value={item.differenceQuantity}
      unit={item.unit}
      className={cn(differs && "rounded bg-amber-100 px-1.5 py-0.5 font-semibold text-amber-800")}
    />
  );
}

/** Bảng đối chiếu tổng xuất kho với lượng ghi trong công việc, theo vật tư và mùa vụ (FR-RPT-SUP-03, BR-RPT-06). */
export function ReconciliationList({ items }: { items: readonly SupplyReconciliationItem[] }) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">Không có lần xuất kho nào trong kỳ.</p>;
  }

  return (
    <>
      <ul className="flex flex-col gap-3 md:hidden">
        {items.map((item) => (
          <li key={`${item.supplyId}-${item.season.seasonId}`} className="flex flex-col gap-2 rounded-lg border p-3">
            <div className="flex flex-col">
              <span className="font-medium">{item.supplyName}</span>
              <span className="text-sm text-muted-foreground">{seasonLabel(item.season)}</span>
            </div>
            <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
              <dt className="text-muted-foreground">Tổng xuất</dt>
              <dd className="text-right">
                <QuantityText value={item.totalOutQuantity} unit={item.unit} />
              </dd>
              <dt className="text-muted-foreground">Ghi trong công việc</dt>
              <dd className="text-right">
                <QuantityText value={item.recordedInTasksQuantity} unit={item.unit} />
              </dd>
              <dt className="text-muted-foreground">Chênh lệch</dt>
              <dd className="text-right">
                <Difference item={item} />
              </dd>
            </dl>
          </li>
        ))}
      </ul>

      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Vật tư</TableHead>
              <TableHead>Mùa vụ</TableHead>
              <TableHead className="text-right">Tổng xuất</TableHead>
              <TableHead className="text-right">Xuất thủ công</TableHead>
              <TableHead className="text-right">Ghi trong công việc</TableHead>
              <TableHead className="text-right">Chênh lệch</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={`${item.supplyId}-${item.season.seasonId}`}>
                <TableCell className="font-medium">{item.supplyName}</TableCell>
                <TableCell>{seasonLabel(item.season)}</TableCell>
                <TableCell className="text-right">
                  <QuantityText value={item.totalOutQuantity} unit={item.unit} />
                </TableCell>
                <TableCell className="text-right">
                  <QuantityText value={item.manualOutQuantity} unit={item.unit} />
                </TableCell>
                <TableCell className="text-right">
                  <QuantityText value={item.recordedInTasksQuantity} unit={item.unit} />
                </TableCell>
                <TableCell className="text-right">
                  <Difference item={item} />
                </TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </>
  );
}

/** Danh sách các lần xuất thủ công trong kỳ để người dùng kiểm tra (BR-RPT-06). */
export function ManualOutList({ items }: { items: readonly SupplyManualOutItem[] }) {
  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">Không có lần xuất thủ công nào trong kỳ.</p>;
  }

  return (
    <>
      <ul className="flex flex-col divide-y md:hidden">
        {items.map((item) => (
          <li key={item.stockTransactionId} className="flex items-start justify-between gap-3 py-2">
            <div className="flex min-w-0 flex-col">
              <span className="font-medium">{item.supplyName}</span>
              <span className="text-sm text-muted-foreground">{seasonLabel(item.season)}</span>
              <span className="text-sm text-muted-foreground">
                <DateText value={item.transactionDate} /> · {item.createdByName}
              </span>
              {item.note && <span className="text-sm text-muted-foreground">{item.note}</span>}
            </div>
            <QuantityText value={item.quantity} unit={item.unit} className="font-medium" />
          </li>
        ))}
      </ul>

      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Ngày</TableHead>
              <TableHead>Vật tư</TableHead>
              <TableHead>Mùa vụ</TableHead>
              <TableHead className="text-right">Lượng</TableHead>
              <TableHead>Người ghi</TableHead>
              <TableHead>Ghi chú</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={item.stockTransactionId}>
                <TableCell>
                  <DateText value={item.transactionDate} />
                </TableCell>
                <TableCell className="font-medium">{item.supplyName}</TableCell>
                <TableCell>{seasonLabel(item.season)}</TableCell>
                <TableCell className="text-right">
                  <QuantityText value={item.quantity} unit={item.unit} />
                </TableCell>
                <TableCell>{item.createdByName}</TableCell>
                <TableCell className="text-muted-foreground">{item.note}</TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </>
  );
}
