import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { PermissionGate } from "@/components/common/PermissionGate";
import { QuantityText } from "@/components/common/QuantityText";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Button, buttonVariants } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { StockLevelBadge } from "@/features/supplies/components/StockLevelBadge";
import { useSupplies } from "@/features/supplies/queries";
import type { SupplyListItem, SupplyQuery } from "@/features/supplies/types";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { recordStatusLabels, supplyGroupLabels } from "@/lib/enum-labels";
import { type RecordStatus, recordStatuses, type SupplyGroup, supplyGroups } from "@/lib/enums";
import { formatQuantity } from "@/lib/format";

/** `/supplies`: danh sách vật tư, tồn, cảnh báo dưới ngưỡng. Bộ lọc, trang hiện tại nằm trên đường dẫn để quay lại vẫn giữ. */
export function SupplyListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const supplies = useSupplies(query);
  const { data: user } = useCurrentUser();
  const canCreate = user !== undefined && canAccess(user, ACCESS.owner);
  const canSeePrices = user !== undefined && canAccess(user, ACCESS.stockManager);

  // Đổi bộ lọc, từ khóa thì về trang 1; chỉ giữ tham số có giá trị để đường dẫn gọn.
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

  const activeFilters: ActiveFilter[] = [
    query.group && {
      key: "group",
      label: `Nhóm: ${supplyGroupLabels[query.group]}`,
      onRemove: () => updateParams({ group: undefined }),
    },
    query.status && {
      key: "status",
      label: `Trạng thái: ${recordStatusLabels[query.status]}`,
      onRemove: () => updateParams({ status: undefined }),
    },
  ].filter((filter) => filter !== undefined);

  const isFiltered = activeFilters.length > 0 || Boolean(query.search) || query.belowMin === true;
  const createButton = (
    <Link to="/supplies/new" className={buttonVariants()}>
      <PlusIcon data-icon="inline-start" />
      Thêm vật tư
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Vật tư" action={<PermissionGate allowed={canCreate}>{createButton}</PermissionGate>} />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo mã, tên"
        activeFilters={activeFilters}
        filters={
          <>
            <FilterSelect
              id="filter-group"
              label="Nhóm"
              value={query.group}
              options={supplyGroups.map((value) => ({ value, label: supplyGroupLabels[value] }))}
              onChange={(group) => updateParams({ group })}
            />
            <FilterSelect
              id="filter-status"
              label="Trạng thái"
              value={query.status}
              options={recordStatuses.map((value) => ({ value, label: recordStatusLabels[value] }))}
              onChange={(status) => updateParams({ status })}
            />
          </>
        }
      />

      <div>
        <Button
          type="button"
          variant={query.belowMin ? "default" : "outline"}
          aria-pressed={query.belowMin === true}
          onClick={() => updateParams({ belowMin: query.belowMin ? undefined : "true" })}
        >
          Dưới ngưỡng
        </Button>
      </div>

      {supplies.isPending ? (
        <LoadingState variant="list" />
      ) : supplies.isError ? (
        <ErrorAlert error={supplies.error} onRetry={() => void supplies.refetch()} />
      ) : supplies.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có vật tư nào." action={canCreate ? createButton : undefined} />
        )
      ) : (
        <>
          <DataList
            items={supplies.data.items}
            getKey={(supply) => supply.id}
            getHref={(supply) => `/supplies/${supply.id}`}
            title={(supply) => supply.name}
            subtitle={supplySummary}
            badge={supplyBadges}
            columns={[
              { header: "Mã", cell: (supply) => supply.code },
              { header: "Tên vật tư", cell: (supply) => supply.name, className: "min-w-48 whitespace-normal break-words" },
              { header: "Nhóm", cell: (supply) => supplyGroupLabels[supply.group] },
              {
                header: "Tồn",
                cell: (supply) => <QuantityText value={supply.stockQuantity} unit={supply.unit} />,
                className: "text-right",
              },
              ...(canSeePrices
                ? [
                    {
                      header: "Giá vốn",
                      cell: (supply: SupplyListItem) =>
                        supply.averageUnitCost === undefined ? "" : <MoneyText amount={supply.averageUnitCost} />,
                      className: "text-right",
                    },
                  ]
                : []),
              { header: "Trạng thái", cell: supplyBadges },
            ]}
          />
          <Pagination meta={supplies.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: mã · nhóm · tồn.
function supplySummary(supply: SupplyListItem): string {
  return [supply.code, supplyGroupLabels[supply.group], formatQuantity(supply.stockQuantity, supply.unit)].join(" · ");
}

function supplyBadges(supply: SupplyListItem) {
  return (
    <div className="flex flex-col items-end gap-1 md:flex-row md:items-center">
      <StatusBadge kind="RecordStatus" value={supply.status} />
      <StockLevelBadge stockQuantity={supply.stockQuantity} minStockLevel={supply.minStockLevel} />
    </div>
  );
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams): SupplyQuery {
  const page = Number(params.get("page"));
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    group: pick(params.get("group"), supplyGroups),
    status: pick(params.get("status"), recordStatuses),
    belowMin: params.get("belowMin") === "true" ? true : undefined,
  };
}

function pick<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}

interface FilterSelectProps<T extends RecordStatus | SupplyGroup> {
  id: string;
  label: string;
  value: T | undefined;
  options: readonly { value: T; label: string }[];
  onChange: (value: T | undefined) => void;
}

function FilterSelect<T extends RecordStatus | SupplyGroup>({ id, label, value, options, onChange }: FilterSelectProps<T>) {
  return (
    <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
      <label htmlFor={id} className="text-sm font-medium md:sr-only">
        {label}
      </label>
      <NativeSelect
        id={id}
        className="w-full md:w-auto"
        value={value ?? ""}
        onChange={(event) => onChange(pick(event.target.value, options.map((option) => option.value)))}
      >
        <NativeSelectOption value="">{`${label}: tất cả`}</NativeSelectOption>
        {options.map((option) => (
          <NativeSelectOption key={option.value} value={option.value}>
            {option.label}
          </NativeSelectOption>
        ))}
      </NativeSelect>
    </div>
  );
}
