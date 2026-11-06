import { Link } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { StockLevelBadge } from "@/features/supplies/components/StockLevelBadge";
import { useSupplies } from "@/features/supplies/queries";
import type { SupplyListItem } from "@/features/supplies/types";
import { unitLabels } from "@/lib/enum-labels";

// Trang "Hôm nay" chỉ hiện vài dòng đầu; vật tư tồn âm xếp trước nên không bị cắt mất.
const MAX_ROWS = 5;

const quantityFormat = new Intl.NumberFormat("vi-VN", { maximumFractionDigits: 3 });

/** Khối "Vật tư dưới ngưỡng" của trang Hôm nay: tên, tồn/ngưỡng, nhãn. Không có vật tư nào dưới ngưỡng thì ẩn cả khối. */
export function LowStockCard() {
  const supplies = useSupplies({ belowMin: true, page: 1, pageSize: MAX_ROWS });

  if (supplies.isPending) {
    return <LoadingState variant="list" />;
  }

  if (supplies.isError) {
    return <ErrorAlert error={supplies.error} onRetry={() => void supplies.refetch()} />;
  }

  if (supplies.data.items.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-sm font-medium text-muted-foreground">Vật tư dưới ngưỡng</h2>
      <ul className="divide-y rounded-lg border bg-card">
        {supplies.data.items.map((supply) => (
          <li key={supply.id}>
            <Link to={`/supplies/${supply.id}`} className="flex min-h-11 items-center gap-3 px-3 py-2 hover:bg-muted">
              <span className="min-w-0 flex-1 break-words">{supply.name}</span>
              <span className="shrink-0 text-sm whitespace-nowrap tabular-nums">{stockOverMinimum(supply)}</span>
              <span className="shrink-0">
                <StockLevelBadge stockQuantity={supply.stockQuantity} minStockLevel={supply.minStockLevel} />
              </span>
            </Link>
          </li>
        ))}
      </ul>
    </section>
  );
}

// Dạng `20/30 kg`.
function stockOverMinimum(supply: SupplyListItem): string {
  return `${quantityFormat.format(supply.stockQuantity)}/${quantityFormat.format(supply.minStockLevel)} ${unitLabels[supply.unit]}`;
}
