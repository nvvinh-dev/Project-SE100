import { MoneyText } from "@/components/common/MoneyText";
import { QuantityText } from "@/components/common/QuantityText";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { ReportSeasonStatusBadge } from "@/features/reports/common/components/ReportSeasonStatusBadge";
import type { FinanceSeasonItem } from "@/features/reports/finance/types";
import { unitLabels } from "@/lib/enum-labels";
import { cn } from "@/lib/utils";

function seasonTitle(item: FinanceSeasonItem) {
  return `${item.season.cropName} · ${item.season.plotCode}`;
}

function ProfitText({ item }: { item: FinanceSeasonItem }) {
  return (
    <span className="inline-flex flex-wrap items-baseline justify-end gap-1">
      <MoneyText amount={item.profit} className={cn("font-semibold", item.profit < 0 && "text-destructive")} />
      {item.season.isProvisional && <span className="text-xs text-muted-foreground">(tạm tính)</span>}
    </span>
  );
}

/** Chi phí trên một đơn vị: số tiền kèm "/kg", "/ha"; chưa chia được (sản lượng 0) thì hiện "—" (FR-RPT-FIN-02). */
function PerUnitMoney({ amount, per }: { amount: number | null; per: string }) {
  if (amount === null) {
    return <span className="text-muted-foreground">—</span>;
  }
  return (
    <span className="whitespace-nowrap">
      <MoneyText amount={amount} />
      <span className="text-muted-foreground">/{per}</span>
    </span>
  );
}

/**
 * Lợi nhuận theo mùa vụ (FR-RPT-FIN-01, FR-RPT-FIN-02, FR-RPT-FIN-04): điện thoại hiện dạng thẻ, từ `md` hiện bảng.
 * Số liệu tính cho cả mùa vụ; mùa vụ chưa chốt ghi "(tạm tính)" (BR-RPT-03).
 */
export function FinanceSeasonList({ items }: { items: readonly FinanceSeasonItem[] }) {
  return (
    <>
      <ul className="flex flex-col gap-3 md:hidden">
        {items.map((item) => (
          <li key={item.season.seasonId} className="flex flex-col gap-2 rounded-lg border p-3">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span className="font-medium">{seasonTitle(item)}</span>
              <ReportSeasonStatusBadge status={item.season.status} isProvisional={false} />
            </div>
            <dl className="grid grid-cols-2 gap-x-3 gap-y-1 text-sm">
              <dt className="text-muted-foreground">Chi phí vật tư</dt>
              <dd className="text-right"><MoneyText amount={item.supplyCost} /></dd>
              <dt className="text-muted-foreground">Chi phí ghi tay</dt>
              <dd className="text-right"><MoneyText amount={item.expenseAmount} /></dd>
              <dt className="text-muted-foreground">Doanh thu</dt>
              <dd className="text-right"><MoneyText amount={item.revenue} /></dd>
              <dt className="text-muted-foreground">Công nợ</dt>
              <dd className="text-right"><MoneyText amount={item.outstandingAmount} /></dd>
              <dt className="text-muted-foreground">Sản lượng</dt>
              <dd className="text-right"><QuantityText value={item.actualYield} unit={item.season.yieldUnit} /></dd>
              <dt className="text-muted-foreground">Chi phí / đơn vị sản lượng</dt>
              <dd className="text-right">
                <PerUnitMoney amount={item.costPerYieldUnit} per={unitLabels[item.season.yieldUnit]} />
              </dd>
              <dt className="text-muted-foreground">Chi phí / ha</dt>
              <dd className="text-right"><PerUnitMoney amount={item.costPerHectare} per="ha" /></dd>
              <dt className="font-medium">Lợi nhuận</dt>
              <dd className="text-right"><ProfitText item={item} /></dd>
            </dl>
          </li>
        ))}
      </ul>

      <div className="hidden md:block">
        <Table>
          <TableHeader>
            <TableRow>
              <TableHead>Mùa vụ</TableHead>
              <TableHead>Trạng thái</TableHead>
              <TableHead className="text-right whitespace-normal">Chi phí vật tư</TableHead>
              <TableHead className="text-right whitespace-normal">Chi phí ghi tay</TableHead>
              <TableHead className="text-right">Doanh thu</TableHead>
              <TableHead className="text-right">Công nợ</TableHead>
              <TableHead className="text-right">Sản lượng</TableHead>
              <TableHead className="text-right whitespace-normal">Chi phí / đơn vị sản lượng</TableHead>
              <TableHead className="text-right whitespace-normal">Chi phí / ha</TableHead>
              <TableHead className="text-right">Lợi nhuận</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {items.map((item) => (
              <TableRow key={item.season.seasonId}>
                <TableCell className="font-medium">{seasonTitle(item)}</TableCell>
                <TableCell>
                  <ReportSeasonStatusBadge status={item.season.status} isProvisional={false} />
                </TableCell>
                <TableCell className="text-right"><MoneyText amount={item.supplyCost} /></TableCell>
                <TableCell className="text-right"><MoneyText amount={item.expenseAmount} /></TableCell>
                <TableCell className="text-right"><MoneyText amount={item.revenue} /></TableCell>
                <TableCell className="text-right"><MoneyText amount={item.outstandingAmount} /></TableCell>
                <TableCell className="text-right">
                  <QuantityText value={item.actualYield} unit={item.season.yieldUnit} />
                </TableCell>
                <TableCell className="text-right">
                  <PerUnitMoney amount={item.costPerYieldUnit} per={unitLabels[item.season.yieldUnit]} />
                </TableCell>
                <TableCell className="text-right"><PerUnitMoney amount={item.costPerHectare} per="ha" /></TableCell>
                <TableCell className="text-right"><ProfitText item={item} /></TableCell>
              </TableRow>
            ))}
          </TableBody>
        </Table>
      </div>
    </>
  );
}
