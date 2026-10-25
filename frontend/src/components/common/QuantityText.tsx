import type { Unit } from "@/lib/enums";
import { formatQuantity } from "@/lib/format";
import { cn } from "@/lib/utils";

interface QuantityTextProps {
  value: number;
  unit: Unit;
  className?: string;
}

/** Số lượng kèm đơn vị dạng `12,5 kg`, chữ số thẳng cột trong bảng. */
export function QuantityText({ value, unit, className }: QuantityTextProps) {
  return <span className={cn("whitespace-nowrap tabular-nums", className)}>{formatQuantity(value, unit)}</span>;
}
