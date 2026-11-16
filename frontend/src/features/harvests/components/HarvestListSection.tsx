import { useCallback, useState } from "react";
import { DataList, type DataListColumn } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { Pagination } from "@/components/common/Pagination";
import { QuantityText } from "@/components/common/QuantityText";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useHarvests } from "@/features/harvests/queries";
import type { HarvestRecordListItem, HarvestRecordQuery } from "@/features/harvests/types";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { qualityGradeLabels } from "@/lib/enum-labels";
import { qualityGrades, type QualityGrade } from "@/lib/enums";
import { formatDate } from "@/lib/format";

export interface HarvestFilterState {
  page?: number;
  search?: string;
  seasonId?: string;
  qualityGrade?: QualityGrade;
  from?: string;
  to?: string;
  includeVoided?: boolean;
}

interface HarvestListSectionProps {
  /** Khóa danh sách vào một mùa vụ cụ thể khi nhúng trong chi tiết mùa vụ. */
  fixedSeasonId?: string;
  controlledState?: HarvestFilterState;
  onControlledChange?: (changes: Partial<HarvestFilterState>) => void;
  emptyMessage?: string;
}

export function HarvestListSection({
  fixedSeasonId,
  controlledState,
  onControlledChange,
  emptyMessage = "Chưa có phiếu thu hoạch nào.",
}: HarvestListSectionProps) {
  const [localState, setLocalState] = useState<HarvestFilterState>({});
  const state = controlledState ?? localState;

  const updateState = useCallback(
    (changes: Partial<HarvestFilterState>) => {
      if (onControlledChange) {
        onControlledChange(changes);
        return;
      }
      setLocalState((prev) => {
        const next = { ...prev, ...changes };
        if (!("page" in changes)) {
          next.page = undefined;
        }
        return next;
      });
    },
    [onControlledChange],
  );

  const effectiveSeasonId = fixedSeasonId ?? state.seasonId;

  const query: HarvestRecordQuery = {
    page: state.page ?? 1,
    pageSize: 20,
    search: state.search || undefined,
    seasonId: effectiveSeasonId || undefined,
    qualityGrade: state.qualityGrade,
    from: state.from || undefined,
    to: state.from && state.to && state.to < state.from ? undefined : state.to || undefined,
    includeVoided: state.includeVoided ? true : undefined,
  };

  const harvestsQuery = useHarvests(query);

  const handleSeasonFilterChange = useCallback(
    (nextSeasonId: string) => {
      updateState({ seasonId: nextSeasonId || undefined });
    },
    [updateState],
  );

  const activeFilters: ActiveFilter[] = [];
  if (!fixedSeasonId && state.seasonId) {
    activeFilters.push({
      key: "seasonId",
      label: "Đang lọc theo mùa vụ",
      onRemove: () => updateState({ seasonId: undefined }),
    });
  }
  if (state.qualityGrade) {
    activeFilters.push({
      key: "qualityGrade",
      label: `Chất lượng: ${qualityGradeLabels[state.qualityGrade]}`,
      onRemove: () => updateState({ qualityGrade: undefined }),
    });
  }
  if (state.from || state.to) {
    activeFilters.push({
      key: "dateRange",
      label: `Ngày thu: ${state.from ? formatDate(state.from) : "..."} → ${state.to ? formatDate(state.to) : "..."}`,
      onRemove: () => updateState({ from: undefined, to: undefined }),
    });
  }
  if (state.includeVoided) {
    activeFilters.push({
      key: "includeVoided",
      label: "Gồm phiếu đã hủy",
      onRemove: () => updateState({ includeVoided: undefined }),
    });
  }

  const isFiltered = Boolean(
    state.search || (!fixedSeasonId && state.seasonId) || state.qualityGrade || state.from || state.to || state.includeVoided,
  );

  const columns: DataListColumn<HarvestRecordListItem>[] = [
    {
      header: "Ngày thu",
      cell: (item) => <DateText value={item.harvestDate} />,
    },
    ...(!fixedSeasonId
      ? [
          {
            header: "Mùa vụ",
            cell: (item: HarvestRecordListItem) => `${item.cropName} · ${item.plotCode}`,
          },
        ]
      : []),
    {
      header: "Chất lượng",
      cell: (item) => <StatusBadge kind="QualityGrade" value={item.qualityGrade} />,
    },
    {
      header: "Sản lượng",
      cell: (item) => <QuantityText value={item.quantity} unit={item.yieldUnit} />,
      className: "text-right font-medium",
    },
    {
      header: "Ghi chú",
      cell: (item) => <span className="text-muted-foreground">{item.note ?? "—"}</span>,
    },
    ...(state.includeVoided
      ? [
          {
            header: "Trạng thái",
            cell: (item: HarvestRecordListItem) => (item.voidedAt ? <StatusBadge kind="Voided" /> : "Hợp lệ"),
          },
        ]
      : []),
  ];

  return (
    <div className="flex flex-col gap-4">
      <SearchFilterBar
        search={state.search ?? ""}
        onSearchChange={(search) => updateState({ search: search || undefined })}
        searchPlaceholder="Tìm cây trồng, mã thửa, ghi chú"
        activeFilters={activeFilters}
        filters={
          <>
            {!fixedSeasonId && (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="filter-harvest-season">Mùa vụ</Label>
                <SeasonSelect
                  id="filter-harvest-season"
                  value={state.seasonId ?? ""}
                  onChange={handleSeasonFilterChange}
                  emptyLabel="Tất cả mùa vụ"
                />
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="filter-harvest-grade">Loại chất lượng</Label>
              <NativeSelect
                id="filter-harvest-grade"
                className="w-full"
                value={state.qualityGrade ?? ""}
                onChange={(e) =>
                  updateState({ qualityGrade: (e.target.value || undefined) as QualityGrade | undefined })
                }
              >
                <NativeSelectOption value="">Tất cả loại chất lượng</NativeSelectOption>
                {qualityGrades.map((grade) => (
                  <NativeSelectOption key={grade} value={grade}>
                    {qualityGradeLabels[grade]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>

            <div className="grid gap-3 sm:grid-cols-2">
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="filter-harvest-from">Từ ngày</Label>
                <Input
                  id="filter-harvest-from"
                  type="date"
                  value={state.from ?? ""}
                  max={state.to}
                  onChange={(e) => updateState({ from: e.target.value || undefined })}
                />
              </div>
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="filter-harvest-to">Đến ngày</Label>
                <Input
                  id="filter-harvest-to"
                  type="date"
                  value={state.to ?? ""}
                  min={state.from}
                  onChange={(e) => updateState({ to: e.target.value || undefined })}
                />
              </div>
            </div>

            <label className="flex min-h-11 cursor-pointer items-center gap-2 text-sm">
              <input
                type="checkbox"
                checked={Boolean(state.includeVoided)}
                onChange={(e) => updateState({ includeVoided: e.target.checked ? true : undefined })}
                className="size-4 accent-primary"
              />
              Hiện phiếu đã hủy
            </label>
          </>
        }
      />

      {harvestsQuery.isPending ? (
        <LoadingState variant="list" />
      ) : harvestsQuery.isError ? (
        <ErrorAlert error={harvestsQuery.error} onRetry={() => void harvestsQuery.refetch()} />
      ) : harvestsQuery.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState
            onClearFilters={() =>
              updateState({
                search: undefined,
                seasonId: undefined,
                qualityGrade: undefined,
                from: undefined,
                to: undefined,
                includeVoided: undefined,
              })
            }
          />
        ) : (
          <EmptyState message={emptyMessage} />
        )
      ) : (
        <>
          <DataList
            items={harvestsQuery.data.items}
            getKey={(item) => item.id}
            getHref={(item) => `/harvests/${item.id}`}
            title={(item) =>
              fixedSeasonId ? (
                <QuantityText value={item.quantity} unit={item.yieldUnit} />
              ) : (
                `${item.cropName} · ${item.plotCode}`
              )
            }
            subtitle={(item) => (
              <div className="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                <DateText value={item.harvestDate} />
                {!fixedSeasonId && (
                  <>
                    <span>·</span>
                    <QuantityText value={item.quantity} unit={item.yieldUnit} />
                  </>
                )}
                {item.note && (
                  <>
                    <span>·</span>
                    <span className="truncate">{item.note}</span>
                  </>
                )}
              </div>
            )}
            badge={(item) => (
              <div className="flex items-center gap-1.5">
                <StatusBadge kind="QualityGrade" value={item.qualityGrade} />
                {item.voidedAt && <StatusBadge kind="Voided" />}
              </div>
            )}
            columns={columns}
          />
          <Pagination meta={harvestsQuery.data.meta} onPageChange={(page) => updateState({ page })} />
        </>
      )}
    </div>
  );
}
