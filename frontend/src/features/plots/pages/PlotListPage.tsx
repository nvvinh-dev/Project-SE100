import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { PermissionGate } from "@/components/common/PermissionGate";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { usePlots } from "@/features/plots/queries";
import type { PlotListItem, PlotQuery } from "@/features/plots/types";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { recordStatusLabels, soilTypeLabels, waterSourceLabels } from "@/lib/enum-labels";
import { type RecordStatus, recordStatuses, type SoilType, soilTypes, type WaterSource, waterSources } from "@/lib/enums";
import { formatArea } from "@/lib/format";

/** `/plots`: danh sách thửa đất (mẫu trang danh sách). Bộ lọc, trang hiện tại nằm trên đường dẫn để quay lại vẫn giữ. */
export function PlotListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const plots = usePlots(query);
  const { data: user } = useCurrentUser();
  const canCreate = user !== undefined && canAccess(user, ACCESS.owner);

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
    query.status && {
      key: "status",
      label: `Tình trạng: ${recordStatusLabels[query.status]}`,
      onRemove: () => updateParams({ status: undefined }),
    },
    query.soilType && {
      key: "soilType",
      label: `Loại đất: ${soilTypeLabels[query.soilType]}`,
      onRemove: () => updateParams({ soilType: undefined }),
    },
    query.waterSource && {
      key: "waterSource",
      label: `Nguồn nước: ${waterSourceLabels[query.waterSource]}`,
      onRemove: () => updateParams({ waterSource: undefined }),
    },
  ].filter((filter) => filter !== undefined);

  const isFiltered = activeFilters.length > 0 || Boolean(query.search);
  const createButton = (
    <Link to="/plots/new" className={buttonVariants()}>
      <PlusIcon data-icon="inline-start" />
      Thêm thửa đất
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Thửa đất" action={<PermissionGate allowed={canCreate}>{createButton}</PermissionGate>} />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo mã, vị trí"
        activeFilters={activeFilters}
        filters={
          <>
            <FilterSelect
              id="filter-status"
              label="Tình trạng"
              value={query.status}
              options={recordStatuses.map((value) => ({ value, label: recordStatusLabels[value] }))}
              onChange={(status) => updateParams({ status })}
            />
            <FilterSelect
              id="filter-soil-type"
              label="Loại đất"
              value={query.soilType}
              options={soilTypes.map((value) => ({ value, label: soilTypeLabels[value] }))}
              onChange={(soilType) => updateParams({ soilType })}
            />
            <FilterSelect
              id="filter-water-source"
              label="Nguồn nước"
              value={query.waterSource}
              options={waterSources.map((value) => ({ value, label: waterSourceLabels[value] }))}
              onChange={(waterSource) => updateParams({ waterSource })}
            />
          </>
        }
      />

      {plots.isPending ? (
        <LoadingState variant="list" />
      ) : plots.isError ? (
        <ErrorAlert error={plots.error} onRetry={() => void plots.refetch()} />
      ) : plots.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có thửa đất nào." action={canCreate ? createButton : undefined} />
        )
      ) : (
        <>
          <DataList
            items={plots.data.items}
            getKey={(plot) => plot.id}
            getHref={(plot) => `/plots/${plot.id}`}
            title={(plot) => plot.code}
            subtitle={plotSummary}
            badge={(plot) => plot.status && <StatusBadge kind="RecordStatus" value={plot.status} />}
            columns={[
              { header: "Mã thửa", cell: (plot) => plot.code },
              { header: "Vị trí", cell: (plot) => plot.locationDescription ?? "" },
              {
                header: "Diện tích",
                cell: (plot) => (plot.areaM2 === null ? "" : formatArea(plot.areaM2)),
                className: "text-right",
              },
              { header: "Loại đất", cell: (plot) => (plot.soilType ? soilTypeLabels[plot.soilType] : "") },
              { header: "Nguồn nước", cell: (plot) => (plot.waterSource ? waterSourceLabels[plot.waterSource] : "") },
              {
                header: "Tình trạng",
                cell: (plot) => plot.status && <StatusBadge kind="RecordStatus" value={plot.status} />,
              },
            ]}
          />
          <Pagination meta={plots.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: diện tích · loại đất · nguồn nước.
function plotSummary(plot: PlotListItem): string {
  return [
    plot.areaM2 === null ? null : formatArea(plot.areaM2),
    plot.soilType ? soilTypeLabels[plot.soilType] : null,
    plot.waterSource ? waterSourceLabels[plot.waterSource] : null,
  ]
    .filter(Boolean)
    .join(" · ");
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams): PlotQuery {
  const page = Number(params.get("page"));
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    status: pick(params.get("status"), recordStatuses),
    soilType: pick(params.get("soilType"), soilTypes),
    waterSource: pick(params.get("waterSource"), waterSources),
  };
}

function pick<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}

interface FilterSelectProps<T extends RecordStatus | SoilType | WaterSource> {
  id: string;
  label: string;
  value: T | undefined;
  options: readonly { value: T; label: string }[];
  onChange: (value: T | undefined) => void;
}

function FilterSelect<T extends RecordStatus | SoilType | WaterSource>({
  id,
  label,
  value,
  options,
  onChange,
}: FilterSelectProps<T>) {
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
