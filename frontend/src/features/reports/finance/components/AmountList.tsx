import { MoneyText } from "@/components/common/MoneyText";
import { cn } from "@/lib/utils";

export interface AmountRow {
  key: string;
  label: string;
  amount: number;
  /** Dòng tổng: in đậm, có đường kẻ phía trên. */
  total?: boolean;
}

/** Bảng hai cột "nhãn - số tiền", đọc được trên màn hình 360px. */
export function AmountList({ rows, emptyMessage }: { rows: readonly AmountRow[]; emptyMessage: string }) {
  if (rows.length === 0) {
    return <p className="text-sm text-muted-foreground">{emptyMessage}</p>;
  }

  return (
    <dl className="flex flex-col">
      {rows.map((row) => (
        <div
          key={row.key}
          className={cn("flex items-baseline justify-between gap-3 py-2", row.total ? "border-t font-semibold" : "border-b border-dashed last:border-b-0")}
        >
          <dt className="min-w-0">{row.label}</dt>
          <dd>
            <MoneyText amount={row.amount} />
          </dd>
        </div>
      ))}
    </dl>
  );
}
