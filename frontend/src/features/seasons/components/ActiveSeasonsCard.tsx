import { Link } from "react-router";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { QuantityText } from "@/components/common/QuantityText";
import { buttonVariants } from "@/components/ui/button";
import { useSeasons } from "@/features/seasons/queries";
import { computeYieldProgressPercent, formatYieldDeviation } from "@/features/seasons/utils";

/** Thẻ "Mùa vụ đang canh tác" cho trang Hôm nay (FR-HAR-02), sẵn sàng ghép ở CHUNG-02. */
export function ActiveSeasonsCard() {
  const seasonsQuery = useSeasons({ status: "InProgress", page: 1, pageSize: 3 });

  if (seasonsQuery.isPending) {
    return (
      <section className="flex flex-col gap-3 rounded-lg border p-4">
        <h2 className="font-semibold">Mùa vụ đang canh tác</h2>
        <LoadingState variant="list" />
      </section>
    );
  }

  if (seasonsQuery.isError) {
    return (
      <section className="flex flex-col gap-3 rounded-lg border p-4">
        <h2 className="font-semibold">Mùa vụ đang canh tác</h2>
        <ErrorAlert error={seasonsQuery.error} onRetry={() => void seasonsQuery.refetch()} />
      </section>
    );
  }

  const items = seasonsQuery.data.items;

  return (
    <section className="flex flex-col gap-3 rounded-lg border p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-semibold">Mùa vụ đang canh tác</h2>
        <Link
          to="/seasons?status=InProgress"
          className={buttonVariants({ variant: "ghost", size: "sm", className: "min-h-11" })}
        >
          Xem tất cả
        </Link>
      </div>

      {items.length === 0 ? (
        <p className="py-1 text-sm text-muted-foreground">Chưa có mùa vụ nào đang canh tác.</p>
      ) : (
        <ul className="divide-y">
          {items.map((season) => {
            const progressPercent = computeYieldProgressPercent(season.harvestedYield, season.targetYield);

            return (
              <li key={season.id}>
                <Link
                  to={`/seasons/${season.id}`}
                  className="flex min-h-11 flex-col gap-1.5 py-2.5 text-sm hover:underline"
                >
                  <div className="flex items-center justify-between gap-2">
                    <span className="font-medium">
                      {season.cropName} · {season.plotCode}
                    </span>
                    <span className="text-xs text-muted-foreground">
                      {season.completedTasksCount}/{season.totalTasksCount} việc
                    </span>
                  </div>
                  <div className="flex flex-wrap items-center justify-between gap-2 text-xs text-muted-foreground">
                    <span>
                      Dự kiến thu <DateText value={season.expectedHarvestDate} />
                    </span>
                    <span>
                      <QuantityText value={season.harvestedYield} unit={season.yieldUnit} /> /{" "}
                      <QuantityText value={season.targetYield} unit={season.yieldUnit} />
                      {season.harvestedYield > 0 && season.targetYield > 0 && (
                        <span className="ml-1">({formatYieldDeviation(season.yieldDeviation)})</span>
                      )}
                    </span>
                  </div>
                  <div className="h-1.5 w-full overflow-hidden rounded-full bg-secondary">
                    <div className="h-full bg-emerald-600" style={{ width: `${progressPercent}%` }} />
                  </div>
                </Link>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
