import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useCustomers } from "@/features/sales/queries";
import type { CustomerQuery } from "@/features/sales/types";
import { recordStatusLabels } from "@/lib/enum-labels";
import { recordStatuses } from "@/lib/enums";
import { pickEnum, usePageParams } from "@/features/sales/use-page-params";

/** `/customers`: danh sách khách hàng của hộ (Chủ hộ, TV+TC). Chạm một dòng mở trang sửa. */
export function CustomerListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const updateParams = usePageParams();
  const query = readQuery(searchParams);
  const customers = useCustomers(query);

  const activeFilters: ActiveFilter[] = [
    query.status && {
      key: "status",
      label: `Tình trạng: ${recordStatusLabels[query.status]}`,
      onRemove: () => updateParams({ status: undefined }),
    },
  ].filter((filter) => filter !== undefined);

  const isFiltered = activeFilters.length > 0 || Boolean(query.search);
  const createButton = (
    <Link to="/customers/new" className={buttonVariants()}>
      <PlusIcon data-icon="inline-start" />
      Thêm khách hàng
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Khách hàng" action={createButton} />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo tên, số điện thoại"
        activeFilters={activeFilters}
        filters={
          <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
            <label htmlFor="filter-status" className="text-sm font-medium md:sr-only">
              Tình trạng
            </label>
            <NativeSelect
              id="filter-status"
              className="w-full md:w-auto"
              value={query.status ?? ""}
              onChange={(event) => updateParams({ status: pickEnum(event.target.value, recordStatuses) })}
            >
              <NativeSelectOption value="">Tình trạng: tất cả</NativeSelectOption>
              {recordStatuses.map((value) => (
                <NativeSelectOption key={value} value={value}>
                  {recordStatusLabels[value]}
                </NativeSelectOption>
              ))}
            </NativeSelect>
          </div>
        }
      />

      {customers.isPending ? (
        <LoadingState variant="list" />
      ) : customers.isError ? (
        <ErrorAlert error={customers.error} onRetry={() => void customers.refetch()} />
      ) : customers.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có khách hàng nào." action={createButton} />
        )
      ) : (
        <>
          <DataList
            items={customers.data.items}
            getKey={(customer) => customer.id}
            getHref={(customer) => `/customers/${customer.id}/edit`}
            title={(customer) => customer.name}
            subtitle={(customer) => customer.phone ?? ""}
            badge={(customer) => <StatusBadge kind="RecordStatus" value={customer.status} />}
            columns={[
              { header: "Tên khách hàng", cell: (customer) => customer.name },
              { header: "Số điện thoại", cell: (customer) => customer.phone ?? "" },
              { header: "Tình trạng", cell: (customer) => <StatusBadge kind="RecordStatus" value={customer.status} /> },
            ]}
          />
          <Pagination meta={customers.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

function readQuery(params: URLSearchParams): CustomerQuery {
  const page = Number(params.get("page"));
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    status: pickEnum(params.get("status"), recordStatuses),
  };
}
