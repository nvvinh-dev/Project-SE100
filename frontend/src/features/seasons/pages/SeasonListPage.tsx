import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { PermissionGate } from "@/components/common/PermissionGate";
import { QuantityText } from "@/components/common/QuantityText";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { usePlots } from "@/features/plots/queries";
import { useSeasons } from "@/features/seasons/queries";
import type { SeasonQuery } from "@/features/seasons/types";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { seasonStatusLabels } from "@/lib/enum-labels";
import { seasonStatuses } from "@/lib/enums";
import { formatDate } from "@/lib/format";

const PAGE_SIZE_OPTIONS = 100;

/** `/seasons`: danh sách mùa vụ (FR-SEA-11). Bộ lọc, trang hiện tại nằm trên đường dẫn để quay lại vẫn giữ. */
export function SeasonListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const seasons = useSeasons(query);

  // Danh sách thửa đất để hiển thị ô lọc
  const plots = usePlots({ page: 1, pageSize: PAGE_SIZE_OPTIONS });

  // Trích xuất danh sách cây trồng từ dữ liệu mùa vụ để lọc khi chưa có API danh mục cây trồng
  // TODO(CROP): Thay bằng useCrops() khi module Crops hoàn thiện API danh mục cây trồng để không phụ thuộc vào 100 mùa vụ đầu tiên.
  const allSeasonsForCrops = useSeasons({ page: 1, pageSize: PAGE_SIZE_OPTIONS });
  const cropMap = new Map<string, string>();
  for (const item of allSeasonsForCrops.data?.items ?? []) {
    if (!cropMap.has(item.cropId)) {
      cropMap.set(item.cropId, item.cropName);
    }
  }
  const cropOptions = Array.from(cropMap.entries()).map(([id, name]) => ({ id, name }));

  const { data: user } = useCurrentUser();
  const canCreate = user !== undefined && canAccess(user, ACCESS.owner);

  // Đổi bộ lọc, từ khóa thì về trang 1; chỉ giữ tham số có giá trị để đường dẫn gọn
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

  const selectedPlot = plots.data?.items.find((p) => p.id === query.plotId);
  const selectedCropName = query.cropId ? (cropMap.get(query.cropId) ?? query.cropId) : undefined;

  const activeFilters: ActiveFilter[] = [
    query.plotId
      ? {
          key: "plotId",
          label: `Thửa: ${selectedPlot ? selectedPlot.code : "Thửa đã chọn"}`,
          onRemove: () => updateParams({ plotId: undefined }),
        }
      : undefined,
    query.cropId
      ? {
          key: "cropId",
          label: `Cây: ${selectedCropName ?? "Cây đã chọn"}`,
          onRemove: () => updateParams({ cropId: undefined }),
        }
      : undefined,
    query.status
      ? {
          key: "status",
          label: `Trạng thái: ${seasonStatusLabels[query.status]}`,
          onRemove: () => updateParams({ status: undefined }),
        }
      : undefined,
    query.from
      ? {
          key: "from",
          label: `Từ: ${formatDate(query.from)}`,
          onRemove: () => updateParams({ from: undefined }),
        }
      : undefined,
    query.to
      ? {
          key: "to",
          label: `Đến: ${formatDate(query.to)}`,
          onRemove: () => updateParams({ to: undefined }),
        }
      : undefined,
  ].filter((filter): filter is ActiveFilter => filter !== undefined);

  const isFiltered = activeFilters.length > 0 || Boolean(query.search);
  const createButton = (
    <Link to="/seasons/new" className={buttonVariants()}>
      <PlusIcon data-icon="inline-start" />
      Thêm mùa vụ
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Mùa vụ" action={<PermissionGate allowed={canCreate}>{createButton}</PermissionGate>} />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo cây, thửa..."
        activeFilters={activeFilters}
        filters={
          <>
            <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
              <label htmlFor="filter-plot" className="text-sm font-medium md:sr-only">
                Thửa đất
              </label>
              <NativeSelect
                id="filter-plot"
                className="w-full md:w-auto"
                value={query.plotId ?? ""}
                onChange={(event) => updateParams({ plotId: event.target.value || undefined })}
              >
                <NativeSelectOption value="">Thửa đất: tất cả</NativeSelectOption>
                {query.plotId && !selectedPlot && (
                  <NativeSelectOption value={query.plotId}>Thửa đã chọn</NativeSelectOption>
                )}
                {plots.data?.items.map((plot) => (
                  <NativeSelectOption key={plot.id} value={plot.id}>
                    {plot.code}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>

            <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
              <label htmlFor="filter-crop" className="text-sm font-medium md:sr-only">
                Cây trồng
              </label>
              <NativeSelect
                id="filter-crop"
                className="w-full md:w-auto"
                value={query.cropId ?? ""}
                onChange={(event) => updateParams({ cropId: event.target.value || undefined })}
              >
                <NativeSelectOption value="">Cây trồng: tất cả</NativeSelectOption>
                {query.cropId && !cropMap.has(query.cropId) && (
                  <NativeSelectOption value={query.cropId}>Cây đã chọn</NativeSelectOption>
                )}
                {cropOptions.map((crop) => (
                  <NativeSelectOption key={crop.id} value={crop.id}>
                    {crop.name}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>

            <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
              <label htmlFor="filter-status" className="text-sm font-medium md:sr-only">
                Trạng thái
              </label>
              <NativeSelect
                id="filter-status"
                className="w-full md:w-auto"
                value={query.status ?? ""}
                onChange={(event) => updateParams({ status: pick(event.target.value, seasonStatuses) })}
              >
                <NativeSelectOption value="">Trạng thái: tất cả</NativeSelectOption>
                {seasonStatuses.map((status) => (
                  <NativeSelectOption key={status} value={status}>
                    {seasonStatusLabels[status]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>

            <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
              <label htmlFor="filter-from" className="text-sm font-medium whitespace-nowrap">
                Từ ngày
              </label>
              <Input
                type="date"
                id="filter-from"
                aria-label="Từ ngày"
                max={query.to}
                className="h-11 w-full md:h-8 md:w-36"
                value={query.from ?? ""}
                onChange={(event) => updateParams({ from: event.target.value || undefined })}
              />
            </div>

            <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
              <label htmlFor="filter-to" className="text-sm font-medium whitespace-nowrap">
                Đến ngày
              </label>
              <Input
                type="date"
                id="filter-to"
                aria-label="Đến ngày"
                min={query.from}
                className="h-11 w-full md:h-8 md:w-36"
                value={query.to ?? ""}
                onChange={(event) => updateParams({ to: event.target.value || undefined })}
              />
            </div>
          </>
        }
      />

      {seasons.isPending ? (
        <LoadingState variant="list" />
      ) : seasons.isError ? (
        <ErrorAlert error={seasons.error} onRetry={() => void seasons.refetch()} />
      ) : seasons.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState
            message="Chưa có mùa vụ nào. Tạo mùa vụ đầu tiên để bắt đầu theo dõi."
            action={canCreate ? createButton : undefined}
          />
        )
      ) : (
        <>
          <DataList
            items={seasons.data.items}
            getKey={(season) => season.id}
            getHref={(season) => `/seasons/${season.id}`}
            title={(season) => `${season.cropName} · ${season.plotCode}`}
            subtitle={(season) => (
              <div className="flex flex-col gap-0.5">
                <div>
                  <DateText value={season.startDate} /> → <DateText value={season.expectedHarvestDate} />
                </div>
                <div>
                  0 / <QuantityText value={season.targetYield} unit={season.yieldUnit} />
                </div>
              </div>
            )}
            badge={(season) => <StatusBadge kind="SeasonStatus" value={season.status} />}
            columns={[
              {
                header: "Mùa vụ",
                cell: (season) => `${season.cropName} · ${season.plotCode}`,
              },
              {
                header: "Thời gian",
                cell: (season) => (
                  <span>
                    <DateText value={season.startDate} /> → <DateText value={season.expectedHarvestDate} />
                  </span>
                ),
              },
              {
                header: "Sản lượng",
                cell: (season) => (
                  <span>
                    0 / <QuantityText value={season.targetYield} unit={season.yieldUnit} />
                  </span>
                ),
                className: "text-right",
              },
              {
                header: "Trạng thái",
                cell: (season) => <StatusBadge kind="SeasonStatus" value={season.status} />,
              },
            ]}
          />
          <Pagination meta={seasons.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

const UUID_PATTERN = /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

// Đọc tham số trên đường dẫn; giá trị lạ thì bỏ qua thay vì gửi lên API
function readQuery(params: URLSearchParams): SeasonQuery {
  const page = Number(params.get("page"));
  const from = matchPattern(params.get("from"), DATE_PATTERN);
  const rawTo = matchPattern(params.get("to"), DATE_PATTERN);
  // Bỏ qua `to` nếu nhỏ hơn `from` để tránh lỗi 400 từ SeasonQueryValidator
  const to = from && rawTo && rawTo < from ? undefined : rawTo;

  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    plotId: matchPattern(params.get("plotId"), UUID_PATTERN),
    cropId: matchPattern(params.get("cropId"), UUID_PATTERN),
    status: pick(params.get("status"), seasonStatuses),
    from,
    to,
  };
}

function matchPattern(value: string | null, pattern: RegExp): string | undefined {
  return value && pattern.test(value) ? value : undefined;
}

function pick<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}
