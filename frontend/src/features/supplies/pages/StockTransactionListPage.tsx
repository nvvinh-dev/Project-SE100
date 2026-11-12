import { useSearchParams } from "react-router";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { StockTransactionList } from "@/features/supplies/components/StockTransactionList";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import { useStockTransactions } from "@/features/supplies/queries";
import type { StockTransactionQuery } from "@/features/supplies/types";
import { stockTransactionTypeLabels } from "@/lib/enum-labels";
import { stockTransactionTypes } from "@/lib/enums";

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;

/**
 * `/stock-transactions`: lịch sử giao dịch kho (Chủ hộ, TV+Kho), mới nhất trước, gồm cả giao dịch đã hủy để thấy nhãn "Đã hủy".
 * API không tìm theo chữ nên các ô lọc đặt thẳng trên trang. Mở từ trang mùa vụ kèm `?seasonId=` thì đã lọc sẵn mùa vụ đó.
 */
export function StockTransactionListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const transactions = useStockTransactions(query);

  // Đổi bộ lọc thì về trang 1; chỉ giữ tham số có giá trị để đường dẫn gọn.
  function updateParams(changes: Record<string, string | undefined>) {
    setSearchParams((params) => {
      const next = new URLSearchParams(params);
      for (const [key, value] of Object.entries(changes)) {
        if (value) {
          next.set(key, value);
        } else {
          next.delete(key);
        }
      }
      if (!("page" in changes)) {
        next.delete("page");
      }
      return next;
    });
  }

  const isFiltered = Boolean(query.supplyId || query.type || query.from || query.to || query.seasonId);

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Lịch sử kho" />

      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-supply" className="text-sm font-medium">
            Vật tư
          </label>
          <SupplySelect
            id="filter-supply"
            value={query.supplyId ?? ""}
            onChange={(supplyId) => updateParams({ supplyId: supplyId || undefined })}
            emptyLabel="Tất cả vật tư"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-season" className="text-sm font-medium">
            Mùa vụ
          </label>
          {/* Không truyền thao tác: lấy mùa vụ mọi trạng thái, kể cả đã chốt; quyền Kho thấy cả mùa vụ ngoài phân công (BR-USR-13). */}
          <SeasonSelect
            id="filter-season"
            value={query.seasonId ?? ""}
            onChange={(seasonId) => updateParams({ seasonId: seasonId || undefined })}
            permission="ManageStock"
            emptyLabel="Tất cả mùa vụ"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-type" className="text-sm font-medium">
            Loại giao dịch
          </label>
          <NativeSelect
            id="filter-type"
            className="w-full"
            value={query.type ?? ""}
            onChange={(event) => updateParams({ type: event.target.value || undefined })}
          >
            <NativeSelectOption value="">Tất cả</NativeSelectOption>
            {stockTransactionTypes.map((type) => (
              <NativeSelectOption key={type} value={type}>
                {stockTransactionTypeLabels[type]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-from" className="text-sm font-medium">
            Từ ngày
          </label>
          <Input
            id="filter-from"
            type="date"
            value={query.from ?? ""}
            max={query.to}
            onChange={(event) => updateParams({ from: event.target.value || undefined })}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-to" className="text-sm font-medium">
            Đến ngày
          </label>
          <Input
            id="filter-to"
            type="date"
            value={query.to ?? ""}
            min={query.from}
            onChange={(event) => updateParams({ to: event.target.value || undefined })}
          />
        </div>
      </div>

      {isFiltered && (
        <div>
          <Button type="button" variant="outline" onClick={() => setSearchParams(new URLSearchParams())}>
            Bỏ lọc
          </Button>
        </div>
      )}

      {transactions.isPending ? (
        <LoadingState variant="list" />
      ) : transactions.isError ? (
        <ErrorAlert error={transactions.error} onRetry={() => void transactions.refetch()} />
      ) : transactions.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có giao dịch kho nào." />
        )
      ) : (
        <>
          <StockTransactionList items={transactions.data.items} />
          <Pagination meta={transactions.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams): StockTransactionQuery {
  const page = Number(params.get("page"));
  const from = params.get("from");
  const to = params.get("to");
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    supplyId: params.get("supplyId") ?? undefined,
    type: stockTransactionTypes.find((type) => type === params.get("type")),
    from: from && DATE_ONLY.test(from) ? from : undefined,
    to: to && DATE_ONLY.test(to) ? to : undefined,
    seasonId: params.get("seasonId") ?? undefined,
    // Luôn gồm giao dịch đã hủy để hiện nhãn "Đã hủy" thay vì để dòng biến mất.
    includeVoided: true,
  };
}
