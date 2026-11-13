import type { ReactNode } from "react";

interface CancelledSeasonsSectionProps<T> {
  items: readonly T[];
  renderItems: (items: readonly T[]) => ReactNode;
  description?: string;
}

/**
 * Khối "Mùa vụ đã hủy" hiển thị tách riêng khỏi bảng chính và không tính vào so sánh năng suất (BR-RPT-03).
 */
export function CancelledSeasonsSection<T>({
  items,
  renderItems,
  description = "Mùa vụ đã hủy hiển thị tách riêng và không tính vào so sánh năng suất.",
}: CancelledSeasonsSectionProps<T>) {
  if (items.length === 0) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3 rounded-lg border bg-muted/30 p-4">
      <div>
        <h2 className="text-base font-semibold">Mùa vụ đã hủy ({items.length})</h2>
        <p className="mt-0.5 text-sm text-muted-foreground">{description}</p>
      </div>
      {renderItems(items)}
    </section>
  );
}
