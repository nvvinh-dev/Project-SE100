import { StatusBadge } from "@/components/common/StatusBadge";

interface StockLevelBadgeProps {
  stockQuantity: number;
  minStockLevel: number;
}

/** Nhãn mức tồn: đỏ "Tồn âm" khi tồn < 0, vàng cam "Sắp hết" khi 0 ≤ tồn < ngưỡng; đủ tồn thì không hiện gì. */
export function StockLevelBadge({ stockQuantity, minStockLevel }: StockLevelBadgeProps) {
  if (stockQuantity < 0) {
    return <StatusBadge kind="NegativeStock" />;
  }
  return stockQuantity < minStockLevel ? <StatusBadge kind="BelowMinStock" /> : null;
}
