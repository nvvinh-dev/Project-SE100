import { PlusIcon } from "lucide-react";
import { useState } from "react";
import { Link } from "react-router";
import { DataList } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { Pagination } from "@/components/common/Pagination";
import { QuantityText } from "@/components/common/QuantityText";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { useSeasons } from "@/features/seasons/queries";
import type { SeasonListItem } from "@/features/seasons/types";

interface PlotSeasonsSectionProps {
  plotId: string;
  /** Chủ hộ: trạng thái trống có nút tạo mùa vụ. */
  canCreate: boolean;
}

/**
 * Khối "Mùa vụ trên thửa" ở trang chi tiết thửa (FR-PLOT-04). Dùng lại API danh sách mùa vụ lọc theo thửa;
 * backend lọc phạm vi nên Thành viên hộ chỉ thấy mùa vụ được phân công (BR-GEN-02).
 */
export function PlotSeasonsSection({ plotId, canCreate }: PlotSeasonsSectionProps) {
  const [page, setPage] = useState(1);
  const seasons = useSeasons({ plotId, page });

  return (
    <section className="flex flex-col gap-3" aria-labelledby="plot-seasons-heading">
      <h2 id="plot-seasons-heading" className="text-lg font-semibold">
        Mùa vụ trên thửa
      </h2>

      {seasons.isPending ? (
        <LoadingState variant="list" />
      ) : seasons.isError ? (
        <ErrorAlert error={seasons.error} onRetry={() => void seasons.refetch()} />
      ) : seasons.data.items.length === 0 ? (
        <EmptyState
          message="Chưa có mùa vụ nào trên thửa này."
          action={
            canCreate ? (
              <Link to="/seasons/new" className={buttonVariants()}>
                <PlusIcon data-icon="inline-start" />
                Thêm mùa vụ
              </Link>
            ) : undefined
          }
        />
      ) : (
        <>
          <DataList
            items={seasons.data.items}
            getKey={(season) => season.id}
            getHref={(season) => `/seasons/${season.id}`}
            title={(season) => season.cropName}
            subtitle={(season) => (
              <div className="flex flex-col gap-0.5">
                <SeasonDates season={season} />
                <SeasonYield season={season} />
              </div>
            )}
            badge={(season) => <StatusBadge kind="SeasonStatus" value={season.status} />}
            columns={[
              { header: "Cây trồng", cell: (season) => season.cropName },
              { header: "Thời gian", cell: (season) => <SeasonDates season={season} /> },
              { header: "Sản lượng", cell: (season) => <SeasonYield season={season} />, className: "text-right" },
              { header: "Trạng thái", cell: (season) => <StatusBadge kind="SeasonStatus" value={season.status} /> },
            ]}
          />
          <Pagination meta={seasons.data.meta} onPageChange={setPage} />
        </>
      )}
    </section>
  );
}

function SeasonDates({ season }: { season: SeasonListItem }) {
  return (
    <span>
      <DateText value={season.startDate} /> → <DateText value={season.expectedHarvestDate} />
    </span>
  );
}

// Sản lượng đã thu so với mục tiêu, cùng đơn vị sản lượng của cây.
function SeasonYield({ season }: { season: SeasonListItem }) {
  return (
    <span>
      <QuantityText value={season.harvestedYield} unit={season.yieldUnit} /> /{" "}
      <QuantityText value={season.targetYield} unit={season.yieldUnit} />
    </span>
  );
}
