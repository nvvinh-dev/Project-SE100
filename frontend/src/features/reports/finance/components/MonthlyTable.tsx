import { MoneyText } from "@/components/common/MoneyText";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { formatMonth } from "@/features/reports/finance/month";
import type { FinanceMonthItem } from "@/features/reports/finance/types";

/** Bảng số liệu doanh thu, chi phí theo tháng nằm ngay dưới biểu đồ (FR-RPT-FIN-06). */
export function MonthlyTable({ items }: { items: readonly FinanceMonthItem[] }) {
  return (
    <Table>
      <TableHeader>
        <TableRow>
          <TableHead>Tháng</TableHead>
          <TableHead className="text-right">Doanh thu</TableHead>
          <TableHead className="text-right">Chi phí</TableHead>
        </TableRow>
      </TableHeader>
      <TableBody>
        {items.map((item) => (
          <TableRow key={item.month}>
            <TableCell>{formatMonth(item.month)}</TableCell>
            <TableCell className="text-right">
              <MoneyText amount={item.revenue} />
            </TableCell>
            <TableCell className="text-right">
              <MoneyText amount={item.cost} />
            </TableCell>
          </TableRow>
        ))}
      </TableBody>
    </Table>
  );
}
