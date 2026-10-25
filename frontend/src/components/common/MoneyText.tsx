import { formatMoney } from "@/lib/format";
import { cn } from "@/lib/utils";

interface MoneyTextProps {
  amount: number;
  className?: string;
}

/** Số tiền dạng `1.250.000 ₫`, không xuống dòng giữa số và ký hiệu, chữ số thẳng cột trong bảng. */
export function MoneyText({ amount, className }: MoneyTextProps) {
  return <span className={cn("whitespace-nowrap tabular-nums", className)}>{formatMoney(amount)}</span>;
}
