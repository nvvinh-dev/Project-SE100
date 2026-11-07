import { useState } from "react";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { Pagination } from "@/components/common/Pagination";
import { StockTransactionList } from "@/features/supplies/components/StockTransactionList";
import { useStockTransactions } from "@/features/supplies/queries";

const PAGE_SIZE = 10;

/** Khối "Lịch sử giao dịch" ở trang chi tiết vật tư: giao dịch của riêng vật tư đó, kể cả giao dịch đã hủy. */
export function SupplyStockHistory({ supplyId }: { supplyId: string }) {
  // Trang của khối này không lưu trên đường dẫn: đường dẫn là của trang chi tiết vật tư.
  const [page, setPage] = useState(1);
  const transactions = useStockTransactions({ supplyId, includeVoided: true, page, pageSize: PAGE_SIZE });

  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-base font-semibold">Lịch sử giao dịch</h2>
      {transactions.isPending ? (
        <LoadingState variant="list" />
      ) : transactions.isError ? (
        <ErrorAlert error={transactions.error} onRetry={() => void transactions.refetch()} />
      ) : transactions.data.items.length === 0 ? (
        <EmptyState message="Vật tư này chưa có giao dịch kho nào." />
      ) : (
        <>
          <StockTransactionList items={transactions.data.items} showSupply={false} />
          <Pagination meta={transactions.data.meta} onPageChange={setPage} />
        </>
      )}
    </section>
  );
}
