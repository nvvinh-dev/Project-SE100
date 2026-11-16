import { DateText } from "@/components/common/DateText";
import { QuantityText } from "@/components/common/QuantityText";
import { useHarvestsForSaleLink } from "@/features/sales/queries";
import type { QualityGrade } from "@/lib/enums";
import { formatDate, formatQuantity } from "@/lib/format";
import { cn } from "@/lib/utils";

interface HarvestLinkPickerProps {
  id: string;
  seasonId: string;
  qualityGrade: QualityGrade | "";
  value: string[];
  onChange: (harvestRecordIds: string[]) => void;
  invalid?: boolean;
}

/**
 * Ô "Liên kết phiếu thu (không bắt buộc)" (FR-SAL-03): chọn nhiều phiếu của đúng mùa vụ và loại đã chọn, chưa hủy
 * (BR-SAL-05). Không ràng buộc số lượng, liên kết chỉ để tham khảo.
 */
export function HarvestLinkPicker({ id, seasonId, qualityGrade, value, onChange, invalid = false }: HarvestLinkPickerProps) {
  const harvests = useHarvestsForSaleLink(seasonId, qualityGrade);

  if (!seasonId || !qualityGrade) {
    return <p className="text-sm text-muted-foreground">Chọn mùa vụ và loại chất lượng để thấy các phiếu thu.</p>;
  }
  if (harvests.isPending) {
    return <p className="text-sm text-muted-foreground">Đang tải phiếu thu...</p>;
  }
  if (harvests.isError) {
    return <p className="text-sm text-destructive">Không tải được danh sách phiếu thu.</p>;
  }
  if (harvests.data.length === 0) {
    return <p className="text-sm text-muted-foreground">Mùa vụ chưa có phiếu thu loại này.</p>;
  }

  function toggle(harvestRecordId: string, checked: boolean) {
    onChange(checked ? [...value, harvestRecordId] : value.filter((item) => item !== harvestRecordId));
  }

  return (
    <ul id={id} aria-invalid={invalid} className="flex flex-col divide-y rounded-lg border">
      {harvests.data.map((harvest) => {
        const checked = value.includes(harvest.id);
        return (
          <li key={harvest.id}>
            <label
              className={cn(
                "flex min-h-11 cursor-pointer items-center gap-3 px-3 py-2 text-sm",
                checked && "bg-primary/5",
              )}
            >
              <input
                type="checkbox"
                checked={checked}
                onChange={(event) => toggle(harvest.id, event.target.checked)}
                aria-label={`Phiếu thu ngày ${formatDate(harvest.harvestDate)}, ${formatQuantity(harvest.quantity, harvest.yieldUnit)}`}
                className="size-4 accent-primary"
              />
              <span className="flex flex-1 items-center justify-between gap-3">
                <DateText value={harvest.harvestDate} />
                <QuantityText value={harvest.quantity} unit={harvest.yieldUnit} />
              </span>
            </label>
          </li>
        );
      })}
    </ul>
  );
}
