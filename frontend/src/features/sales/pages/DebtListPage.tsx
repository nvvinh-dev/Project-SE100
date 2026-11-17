import { DataList } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { buyerLabel, seasonShortLabel } from "@/features/sales/components/sale-labels";
import { useSaleDebts } from "@/features/sales/queries";
import type { SaleListItem } from "@/features/sales/types";

/** `/debts`: công nợ bán hàng chưa thu (Chủ hộ, TV+TC), nợ lâu nhất trước; chạm một dòng mở giao dịch bán. */
export function DebtListPage() {
  const debts = useSaleDebts();

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Công nợ" />

      {debts.isPending ? (
        <LoadingState variant="list" />
      ) : debts.isError ? (
        <ErrorAlert error={debts.error} onRetry={() => void debts.refetch()} />
      ) : (
        <>
          <section className="grid grid-cols-2 gap-3">
            <div className="rounded-lg border p-4">
              <p className="text-sm text-muted-foreground">Số giao dịch còn nợ</p>
              <p className="text-xl font-semibold">{debts.data.totalCount}</p>
            </div>
            <div className="rounded-lg border p-4">
              <p className="text-sm text-muted-foreground">Tổng còn nợ</p>
              <p className="text-xl font-semibold">
                <MoneyText amount={debts.data.totalOutstanding} />
              </p>
            </div>
          </section>

          {debts.data.items.length === 0 ? (
            <EmptyState message="Không còn công nợ chưa thu." />
          ) : (
            <DataList
              items={debts.data.items}
              getKey={(item) => item.id}
              getHref={(item) => `/sales/${item.id}`}
              title={(item) => buyerLabel(item)}
              subtitle={(item) => <Summary item={item} />}
              badge={(item) => <MoneyText amount={item.outstandingAmount} className="font-semibold" />}
              columns={[
                { header: "Ngày bán", cell: (item) => <DateText value={item.saleDate} /> },
                { header: "Mùa vụ", cell: (item) => seasonShortLabel(item), className: "whitespace-normal" },
                { header: "Khách hàng hoặc kênh", cell: (item) => buyerLabel(item), className: "whitespace-normal" },
                { header: "Doanh thu", cell: (item) => <MoneyText amount={item.revenue} />, className: "text-right" },
                { header: "Đã thu", cell: (item) => <MoneyText amount={item.paidAmount} />, className: "text-right" },
                {
                  header: "Còn nợ",
                  cell: (item) => <MoneyText amount={item.outstandingAmount} />,
                  className: "text-right font-semibold",
                },
              ]}
            />
          )}
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: ngày bán · mùa vụ · doanh thu.
function Summary({ item }: { item: SaleListItem }) {
  return (
    <span>
      <DateText value={item.saleDate} /> · {seasonShortLabel(item)} · Doanh thu <MoneyText amount={item.revenue} />
    </span>
  );
}
