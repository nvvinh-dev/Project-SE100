import { useState } from "react";
import { Link } from "react-router";
import { DataList } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { MoneyText } from "@/components/common/MoneyText";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showSuccess, showWarnings } from "@/components/common/toast";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Button, buttonVariants } from "@/components/ui/button";
import { useVoidStockTransaction } from "@/features/supplies/queries";
import type { StockTransactionListItem } from "@/features/supplies/types";
import { ApiError, getErrorMessage } from "@/lib/api-error";
import { stockTransactionTypeLabels } from "@/lib/enum-labels";
import { formatDate, formatQuantity } from "@/lib/format";

interface StockTransactionListProps {
  items: readonly StockTransactionListItem[];
  /** Lịch sử ở trang chi tiết vật tư không cần lặp lại tên vật tư ở mỗi dòng. */
  showSupply?: boolean;
}

/**
 * Các dòng của lịch sử giao dịch kho kèm thao tác "Hủy giao dịch" (FR-SUP-08). Giao dịch đã hủy, giao dịch sinh từ công việc
 * không có nút hủy; giao dịch của công việc ghi chú "Sửa qua công việc" (BR-TSK-11).
 */
export function StockTransactionList({ items, showSupply = true }: StockTransactionListProps) {
  const voidTransaction = useVoidStockTransaction();
  const [voiding, setVoiding] = useState<StockTransactionListItem | null>(null);
  const [voidError, setVoidError] = useState<unknown>(null);
  // BR-SUP-10: không hủy được giao dịch làm tăng tồn khi đã có thao tác sau nó; hướng dẫn tạo điều chỉnh tồn.
  const [notLast, setNotLast] = useState<{ supplyId: string; message: string } | null>(null);

  function openVoid(item: StockTransactionListItem) {
    setNotLast(null);
    setVoidError(null);
    voidTransaction.reset();
    setVoiding(item);
  }

  async function confirmVoid(reason: string) {
    if (!voiding) {
      return;
    }
    setVoidError(null);
    try {
      const { warnings } = await voidTransaction.mutateAsync({ id: voiding.id, request: { reason } });
      showSuccess("Đã hủy giao dịch kho.");
      showWarnings(warnings);
      setVoiding(null);
    } catch (error) {
      if (error instanceof ApiError && error.code === "STOCK_TX_NOT_LAST") {
        setNotLast({ supplyId: voiding.supplyId, message: getErrorMessage(error) });
        setVoiding(null);
        return;
      }
      // Lỗi khác hiện trong hộp thoại, giữ nguyên lý do đã nhập.
      setVoidError(error);
    }
  }

  function action(item: StockTransactionListItem) {
    if (item.voidedAt) {
      return <StatusBadge kind="Voided" />;
    }
    if (item.isFromFarmTask) {
      return <span className="text-sm whitespace-nowrap text-muted-foreground">Sửa qua công việc</span>;
    }
    return (
      <Button type="button" variant="outline" size="sm" className="min-h-11 md:min-h-8" onClick={() => openVoid(item)}>
        Hủy giao dịch
      </Button>
    );
  }

  return (
    <>
      {notLast && (
        <div className="flex flex-col gap-2">
          <Alert variant="destructive">
            <AlertDescription>{notLast.message}</AlertDescription>
          </Alert>
          {/* Đặt ngoài khung lỗi để giữ đúng kiểu nút và vùng bấm 44px. */}
          <Link
            to={`/stock/adjust?supplyId=${notLast.supplyId}`}
            className={buttonVariants({ variant: "outline", className: "min-h-11 self-start" })}
          >
            Điều chỉnh tồn
          </Link>
        </div>
      )}

      <DataList
        items={items}
        getKey={(item) => item.id}
        title={(item) => (showSupply ? `${stockTransactionTypeLabels[item.type]} · ${item.supplyName}` : stockTransactionTypeLabels[item.type])}
        subtitle={(item) => <Summary item={item} />}
        badge={action}
        columns={[
          { header: "Ngày", cell: (item) => <DateText value={item.transactionDate} /> },
          ...(showSupply
            ? [{ header: "Vật tư", cell: (item: StockTransactionListItem) => item.supplyName, className: "whitespace-normal" }]
            : []),
          { header: "Loại", cell: (item) => stockTransactionTypeLabels[item.type] },
          {
            header: "Số lượng",
            cell: (item) => <span className="whitespace-nowrap tabular-nums">{signedQuantity(item)}</span>,
            className: "text-right",
          },
          { header: "Đơn giá", cell: (item) => <MoneyText amount={item.unitCost} />, className: "text-right" },
          { header: "Giá trị", cell: (item) => <MoneyText amount={item.totalCost} />, className: "text-right" },
          { header: "Mùa vụ", cell: (item) => seasonLabel(item) ?? "", className: "whitespace-normal" },
          { header: "Lý do", cell: (item) => item.reason ?? "", className: "whitespace-normal" },
          { header: "", cell: action },
        ]}
      />

      <ReasonDialog
        open={voiding !== null}
        onOpenChange={(open) => !open && setVoiding(null)}
        title="Hủy giao dịch kho"
        description={voiding ? voidDescription(voiding) : undefined}
        confirmLabel="Hủy giao dịch"
        onConfirm={(reason) => void confirmVoid(reason)}
        pending={voidTransaction.isPending}
        error={voidError}
        destructive
      />
    </>
  );
}

// Dòng phụ của thẻ trên điện thoại: ngày · số lượng · giá trị, rồi mùa vụ, lý do nếu có.
function Summary({ item }: { item: StockTransactionListItem }) {
  const season = seasonLabel(item);
  return (
    <div className="flex flex-col gap-0.5">
      <span>
        <DateText value={item.transactionDate} /> · <span className="tabular-nums">{signedQuantity(item)}</span> ·{" "}
        <MoneyText amount={item.totalCost} />
      </span>
      {season && <span>{season}</span>}
      {item.reason && <span>Lý do: {item.reason}</span>}
    </div>
  );
}

// Nhập, điều chỉnh tăng có dấu "+"; xuất, điều chỉnh giảm đã mang dấu "-" từ API.
function signedQuantity(item: StockTransactionListItem): string {
  const text = formatQuantity(item.quantity, item.unit);
  return item.quantity > 0 ? `+${text}` : text;
}

// Mùa vụ hiển thị rút gọn "Cây · Thửa · ngày bắt đầu".
function seasonLabel(item: StockTransactionListItem): string | null {
  if (!item.seasonId) {
    return null;
  }
  return [item.cropName, item.plotCode, item.seasonStartDate && formatDate(item.seasonStartDate)].filter(Boolean).join(" · ");
}

function voidDescription(item: StockTransactionListItem): string {
  // Giữ dấu để điều chỉnh tăng, giảm không bị đọc lẫn ("Điều chỉnh -2 kg").
  const what = `${stockTransactionTypeLabels[item.type]} ${signedQuantity(item)} ${item.supplyName}`;
  return `${what} ngày ${formatDate(item.transactionDate)} sẽ không còn tính vào tồn và chi phí. Không hoàn tác được.`;
}
