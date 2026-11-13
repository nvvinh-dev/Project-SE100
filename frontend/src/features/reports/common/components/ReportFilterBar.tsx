import { ListFilterIcon, RotateCcwIcon, XIcon } from "lucide-react";
import { type ReactNode, useState, useSyncExternalStore } from "react";
import type { ActiveFilter } from "@/components/common/SearchFilterBar";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useCrops } from "@/features/crops/queries";
import { usePlots } from "@/features/plots/queries";
import type { ReportFilter } from "@/features/reports/common/types";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { useSeasonOptions } from "@/features/seasons/queries";
import type { ExtraPermission } from "@/lib/enums";
import { formatDate } from "@/lib/format";

export type ReportFilterField = "dateRange" | "season" | "plot" | "crop";

const DEFAULT_FIELDS: readonly ReportFilterField[] = ["dateRange", "season", "plot", "crop"];
const DESKTOP_QUERY = "(min-width: 768px)";
const OPTION_PAGE_SIZE = 100;

function subscribeDesktop(onChange: () => void) {
  const query = window.matchMedia(DESKTOP_QUERY);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

// Ô lọc chỉ dựng ở một nơi (hộp thoại trên điện thoại hoặc thanh lọc trên máy tính) để tránh trùng id trên DOM.
function useIsDesktop() {
  return useSyncExternalStore(subscribeDesktop, () => window.matchMedia(DESKTOP_QUERY).matches);
}

interface ReportFilterBarProps {
  filter: ReportFilter;
  onChange: (changes: Partial<ReportFilter>) => void;
  onReset?: () => void;
  fields?: readonly ReportFilterField[];
  /** Quyền mở rộng phạm vi khi chọn mùa vụ theo BR-USR-13 (ManageStock cho báo cáo vật tư, ManageFinance cho tài chính). */
  seasonPermission?: ExtraPermission;
  extraFilters?: ReactNode;
  extraActiveFilters?: readonly ActiveFilter[];
}

/**
 * Thanh bộ lọc dùng chung của các màn hình báo cáo (FR-RPT-01):
 * - Điện thoại (360px): nút "Lọc (n)" mở hộp thoại + danh sách nhãn bộ lọc đang áp dụng.
 * - Máy tính (>= md): hiển thị đầy đủ trên thanh bộ lọc.
 */
export function ReportFilterBar({
  filter,
  onChange,
  onReset,
  fields = DEFAULT_FIELDS,
  seasonPermission,
  extraFilters,
  extraActiveFilters = [],
}: ReportFilterBarProps) {
  const isDesktop = useIsDesktop();
  const [filterOpen, setFilterOpen] = useState(false);

  const hasSeasonField = fields.includes("season");
  const hasPlotField = fields.includes("plot");
  const hasCropField = fields.includes("crop");

  const plotsQuery = usePlots({ page: 1, pageSize: OPTION_PAGE_SIZE });
  const cropsQuery = useCrops({ page: 1, pageSize: OPTION_PAGE_SIZE });
  const seasonsQuery = useSeasonOptions({ permission: seasonPermission });

  const plots = hasPlotField ? (plotsQuery.data?.items ?? []) : [];
  const crops = hasCropField ? (cropsQuery.data?.items ?? []) : [];
  const seasonOptions = hasSeasonField ? (seasonsQuery.data ?? []) : [];

  const activeFilters: ActiveFilter[] = [];

  if (fields.includes("dateRange")) {
    if (filter.from) {
      activeFilters.push({
        key: "from",
        label: `Từ: ${formatDate(filter.from)}`,
        onRemove: () => onChange({ from: undefined }),
      });
    }
    if (filter.to) {
      activeFilters.push({
        key: "to",
        label: `Đến: ${formatDate(filter.to)}`,
        onRemove: () => onChange({ to: undefined }),
      });
    }
  }

  if (hasSeasonField && filter.seasonId) {
    const matchedSeason = seasonOptions.find((item) => item.id === filter.seasonId);
    activeFilters.push({
      key: "seasonId",
      label: `Mùa vụ: ${matchedSeason ? matchedSeason.label : "Đã chọn"}`,
      onRemove: () => onChange({ seasonId: undefined }),
    });
  }

  if (hasPlotField && filter.plotId) {
    const matchedPlot = plots.find((item) => item.id === filter.plotId);
    activeFilters.push({
      key: "plotId",
      label: `Thửa: ${matchedPlot ? matchedPlot.code : "Đã chọn"}`,
      onRemove: () => onChange({ plotId: undefined }),
    });
  }

  if (hasCropField && filter.cropId) {
    const matchedCrop = crops.find((item) => item.id === filter.cropId);
    activeFilters.push({
      key: "cropId",
      label: `Cây trồng: ${matchedCrop ? matchedCrop.name : "Đã chọn"}`,
      onRemove: () => onChange({ cropId: undefined }),
    });
  }

  activeFilters.push(...extraActiveFilters);
  const filterCount = activeFilters.length;

  const filterControls = (
    <>
      {fields.includes("dateRange") && (
        <div className="grid grid-cols-1 gap-3 md:flex md:items-center md:gap-2">
          <div className="flex flex-col gap-1.5 md:flex-row md:items-center">
            <label htmlFor="report-filter-from" className="text-sm font-medium md:whitespace-nowrap">
              Từ ngày
            </label>
            <Input
              id="report-filter-from"
              type="date"
              className="min-h-11 md:min-h-9 md:w-40"
              value={filter.from ?? ""}
              onChange={(event) => onChange({ from: event.target.value || undefined })}
            />
          </div>
          <div className="flex flex-col gap-1.5 md:flex-row md:items-center">
            <label htmlFor="report-filter-to" className="text-sm font-medium md:whitespace-nowrap">
              Đến ngày
            </label>
            <Input
              id="report-filter-to"
              type="date"
              className="min-h-11 md:min-h-9 md:w-40"
              value={filter.to ?? ""}
              onChange={(event) => onChange({ to: event.target.value || undefined })}
            />
          </div>
        </div>
      )}

      {hasSeasonField && (
        <div className="flex flex-col gap-1.5 md:min-w-64">
          <label htmlFor="report-filter-season" className="text-sm font-medium md:sr-only">
            Mùa vụ
          </label>
          <SeasonSelect
            id="report-filter-season"
            value={filter.seasonId ?? ""}
            onChange={(seasonId) => onChange({ seasonId: seasonId || undefined })}
            permission={seasonPermission}
            emptyLabel="Tất cả mùa vụ"
          />
        </div>
      )}

      {hasPlotField && (
        <div className="flex flex-col gap-1.5 md:w-auto">
          <label htmlFor="report-filter-plot" className="text-sm font-medium md:sr-only">
            Thửa đất
          </label>
          <NativeSelect
            id="report-filter-plot"
            className="min-h-11 w-full md:min-h-9 md:w-auto"
            value={filter.plotId ?? ""}
            onChange={(event) => onChange({ plotId: event.target.value || undefined })}
          >
            <NativeSelectOption value="">Thửa đất: tất cả</NativeSelectOption>
            {plots.map((plot) => (
              <NativeSelectOption key={plot.id} value={plot.id}>
                {plot.code} {plot.locationDescription ? `(${plot.locationDescription})` : ""}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
      )}

      {hasCropField && (
        <div className="flex flex-col gap-1.5 md:w-auto">
          <label htmlFor="report-filter-crop" className="text-sm font-medium md:sr-only">
            Cây trồng
          </label>
          <NativeSelect
            id="report-filter-crop"
            className="min-h-11 w-full md:min-h-9 md:w-auto"
            value={filter.cropId ?? ""}
            onChange={(event) => onChange({ cropId: event.target.value || undefined })}
          >
            <NativeSelectOption value="">Cây trồng: tất cả</NativeSelectOption>
            {crops.map((crop) => (
              <NativeSelectOption key={crop.id} value={crop.id}>
                {crop.name}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
      )}

      {extraFilters}
    </>
  );

  return (
    <div className="flex flex-col gap-2">
      {isDesktop ? (
        <div className="flex flex-wrap items-center gap-2 rounded-lg border bg-card p-3">
          {filterControls}
          {filterCount > 0 && onReset && (
            <Button type="button" variant="ghost" size="sm" onClick={onReset}>
              <RotateCcwIcon data-icon="inline-start" />
              Bỏ lọc
            </Button>
          )}
        </div>
      ) : (
        <div className="flex items-center gap-2">
          <Button type="button" variant="outline" className="min-h-11" onClick={() => setFilterOpen(true)}>
            <ListFilterIcon data-icon="inline-start" />
            {filterCount > 0 ? `Lọc (${filterCount})` : "Lọc"}
          </Button>
          {filterCount > 0 && onReset && (
            <Button type="button" variant="ghost" className="min-h-11" onClick={onReset}>
              Bỏ lọc
            </Button>
          )}
        </div>
      )}

      {!isDesktop && filterCount > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label="Bộ lọc đang áp dụng">
          {activeFilters.map((item) => (
            <li key={item.key}>
              <button
                type="button"
                onClick={item.onRemove}
                aria-label={`Bỏ lọc ${item.label}`}
                className="inline-flex min-h-11 items-center gap-1.5 rounded-full border bg-muted px-3 text-sm"
              >
                {item.label}
                <XIcon className="size-4 shrink-0" />
              </button>
            </li>
          ))}
        </ul>
      )}

      {!isDesktop && (
        <Dialog open={filterOpen} onOpenChange={setFilterOpen}>
          <DialogContent showCloseButton={false} aria-describedby={undefined}>
            <DialogHeader>
              <DialogTitle>Lọc báo cáo</DialogTitle>
            </DialogHeader>
            <div className="flex max-h-[70vh] flex-col gap-4 overflow-y-auto py-1">{filterControls}</div>
            <DialogFooter className="gap-2">
              {filterCount > 0 && onReset && (
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => {
                    onReset();
                    setFilterOpen(false);
                  }}
                >
                  Bỏ lọc
                </Button>
              )}
              <Button type="button" onClick={() => setFilterOpen(false)}>
                Xem kết quả
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}
