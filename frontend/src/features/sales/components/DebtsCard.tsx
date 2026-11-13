import { ChevronRightIcon } from "lucide-react";
import { Link } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { MoneyText } from "@/components/common/MoneyText";
import { Skeleton } from "@/components/ui/skeleton";
import { useSaleDebts } from "@/features/sales/queries";

/**
 * Khối "Công nợ chưa thu" của trang Hôm nay (FR-SAL-07): "3 giao dịch · 4.500.000 ₫", chạm mở danh sách công nợ.
 * Trang ghép khối chỉ hiện khối này cho Chủ hộ, TV+TC.
 */
export function DebtsCard() {
  const debts = useSaleDebts();

  return (
    <section className="flex flex-col gap-2">
      <h2 className="text-sm font-semibold tracking-wide text-muted-foreground uppercase">Công nợ chưa thu</h2>
      {debts.isPending ? (
        <Skeleton className="h-14 w-full rounded-lg" />
      ) : debts.isError ? (
        <ErrorAlert error={debts.error} onRetry={() => void debts.refetch()} />
      ) : (
        <Link
          to="/debts"
          className="flex min-h-14 items-center justify-between gap-3 rounded-lg border px-4 py-3 hover:bg-muted"
        >
          {debts.data.totalCount === 0 ? (
            <span className="text-muted-foreground">Không còn công nợ chưa thu.</span>
          ) : (
            <span>
              {debts.data.totalCount} giao dịch · <MoneyText amount={debts.data.totalOutstanding} className="font-semibold" />
            </span>
          )}
          <ChevronRightIcon aria-label="Xem danh sách công nợ" className="size-4 shrink-0 text-muted-foreground" />
        </Link>
      )}
    </section>
  );
}
