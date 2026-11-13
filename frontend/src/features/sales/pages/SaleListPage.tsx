import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { buyerLabel, seasonShortLabel } from "@/features/sales/components/sale-labels";
import { useCustomers, useSales } from "@/features/sales/queries";
import type { SaleListItem, SaleQuery } from "@/features/sales/types";
import { pickEnum, usePageParams } from "@/features/sales/use-page-params";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { paymentStatusLabels, salesChannelLabels } from "@/lib/enum-labels";
import { paymentStatuses, salesChannels } from "@/lib/enums";

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;

/**
 * `/sales`: danh sách giao dịch bán (Chủ hộ, TV+TC), mới nhất trước, gồm cả giao dịch đã hủy để thấy nhãn "Đã hủy".
 * Bộ lọc nằm trên đường dẫn; `?seasonId=` lọc theo mùa vụ khi mở từ trang mùa vụ.
 */
export function SaleListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const updateParams = usePageParams();
  const query = readQuery(searchParams);
  const sales = useSales(query);
  // Ô lọc gồm cả khách hàng đã ngừng sử dụng: giao dịch cũ vẫn tham chiếu tới họ.
  const customers = useCustomers({ pageSize: 100 });

  const isFiltered = Boolean(
    query.seasonId || query.customerId || query.channel || query.paymentStatus || query.from || query.to,
  );
  const createLink = (
    <Link to="/sales/new" className={buttonVariants({ className: "min-h-11" })}>
      <PlusIcon data-icon="inline-start" />
      Bán hàng
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Bán hàng" action={createLink} />

      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-3">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-season" className="text-sm font-medium">
            Mùa vụ
          </label>
          <SeasonSelect
            id="filter-season"
            value={query.seasonId ?? ""}
            onChange={(seasonId) => updateParams({ seasonId: seasonId || undefined })}
            permission="ManageFinance"
            emptyLabel="Tất cả mùa vụ"
          />
        </div>
        <FilterSelect
          id="filter-customer"
          label="Khách hàng"
          value={query.customerId}
          options={(customers.data?.items ?? []).map((customer) => ({ value: customer.id, label: customer.name }))}
          onChange={(customerId) => updateParams({ customerId })}
        />
        <FilterSelect
          id="filter-channel"
          label="Kênh bán"
          value={query.channel}
          options={salesChannels.map((value) => ({ value, label: salesChannelLabels[value] }))}
          onChange={(channel) => updateParams({ channel })}
        />
        <FilterSelect
          id="filter-payment-status"
          label="Trạng thái thanh toán"
          value={query.paymentStatus}
          options={paymentStatuses.map((value) => ({ value, label: paymentStatusLabels[value] }))}
          onChange={(paymentStatus) => updateParams({ paymentStatus })}
        />
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

      {sales.isPending ? (
        <LoadingState variant="list" />
      ) : sales.isError ? (
        <ErrorAlert error={sales.error} onRetry={() => void sales.refetch()} />
      ) : sales.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có giao dịch bán nào." action={createLink} />
        )
      ) : (
        <>
          <DataList
            items={sales.data.items}
            getKey={(item) => item.id}
            getHref={(item) => `/sales/${item.id}`}
            title={(item) => seasonShortLabel(item)}
            subtitle={(item) => <Summary item={item} />}
            badge={(item) => <SaleBadge item={item} />}
            columns={[
              { header: "Ngày bán", cell: (item) => <DateText value={item.saleDate} /> },
              { header: "Cây · Thửa", cell: (item) => seasonShortLabel(item), className: "whitespace-normal" },
              { header: "Khách hàng hoặc kênh", cell: (item) => buyerLabel(item), className: "whitespace-normal" },
              { header: "Doanh thu", cell: (item) => <MoneyText amount={item.revenue} />, className: "text-right" },
              { header: "Thanh toán", cell: (item) => <SaleBadge item={item} /> },
            ]}
          />
          <Pagination meta={sales.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

function SaleBadge({ item }: { item: SaleListItem }) {
  return item.voidedAt ? <StatusBadge kind="Voided" /> : <StatusBadge kind="PaymentStatus" value={item.paymentStatus} />;
}

// Dòng phụ của thẻ trên điện thoại: ngày bán · người mua · doanh thu.
function Summary({ item }: { item: SaleListItem }) {
  return (
    <span>
      <DateText value={item.saleDate} /> · {buyerLabel(item)} ·{" "}
      <MoneyText amount={item.revenue} className={item.voidedAt ? "line-through" : undefined} />
    </span>
  );
}

function readQuery(params: URLSearchParams): SaleQuery {
  const page = Number(params.get("page"));
  const from = params.get("from");
  const to = params.get("to");
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    seasonId: params.get("seasonId") ?? undefined,
    customerId: params.get("customerId") ?? undefined,
    channel: pickEnum(params.get("channel"), salesChannels),
    paymentStatus: pickEnum(params.get("paymentStatus"), paymentStatuses),
    from: from && DATE_ONLY.test(from) ? from : undefined,
    to: to && DATE_ONLY.test(to) ? to : undefined,
    // Luôn gồm giao dịch đã hủy để hiện nhãn "Đã hủy" thay vì để dòng biến mất.
    includeVoided: true,
  };
}

interface FilterSelectProps<T extends string> {
  id: string;
  label: string;
  value: T | undefined;
  options: readonly { value: T; label: string }[];
  onChange: (value: T | undefined) => void;
}

function FilterSelect<T extends string>({ id, label, value, options, onChange }: FilterSelectProps<T>) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      <NativeSelect
        id={id}
        className="w-full"
        value={value ?? ""}
        onChange={(event) => onChange(options.find((option) => option.value === event.target.value)?.value)}
      >
        <NativeSelectOption value="">Tất cả</NativeSelectOption>
        {options.map((option) => (
          <NativeSelectOption key={option.value} value={option.value}>
            {option.label}
          </NativeSelectOption>
        ))}
      </NativeSelect>
    </div>
  );
}
