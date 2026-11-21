import { Link } from "react-router";
import { QuantityText } from "@/components/common/QuantityText";
import { ReportSection } from "@/features/reports/finance/components/ReportSection";
import type { SupplyReportLowStockItem } from "@/features/reports/supplies/types";
import { StockLevelBadge } from "@/features/supplies/components/StockLevelBadge";
import { supplyGroupLabels } from "@/lib/enum-labels";

/** Khối "Vật tư dưới ngưỡng" ở đầu báo cáo vật tư (FR-RPT-SUP-02, BR-SUP-12); tồn âm đã được xếp trước. */
export function LowStockSection({ items }: { items: readonly SupplyReportLowStockItem[] }) {
  return (
    <ReportSection title="Vật tư dưới ngưỡng" description="Tồn hiện tại thấp hơn ngưỡng tối thiểu, tồn âm xếp trước.">
      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">Không có vật tư nào dưới ngưỡng.</p>
      ) : (
        <ul className="flex flex-col divide-y">
          {items.map((item) => (
            <li key={item.supplyId}>
              <Link
                to={`/supplies/${item.supplyId}`}
                className="flex min-h-11 items-center justify-between gap-3 py-2 hover:bg-muted/40"
              >
                <div className="flex min-w-0 flex-col">
                  <span className="truncate font-medium">{item.name}</span>
                  <span className="text-sm text-muted-foreground">
                    {supplyGroupLabels[item.group]} · Tồn <QuantityText value={item.stockQuantity} unit={item.unit} /> /
                    ngưỡng <QuantityText value={item.minStockLevel} unit={item.unit} />
                  </span>
                </div>
                <StockLevelBadge stockQuantity={item.stockQuantity} minStockLevel={item.minStockLevel} />
              </Link>
            </li>
          ))}
        </ul>
      )}
    </ReportSection>
  );
}
