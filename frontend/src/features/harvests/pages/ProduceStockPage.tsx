import { BanIcon, SlidersHorizontalIcon } from "lucide-react";
import { useCallback, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { QuantityText } from "@/components/common/QuantityText";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import {
  useProduceStockAdjustments,
  useProduceStockOverview,
  useVoidProduceStockAdjustment,
} from "@/features/harvests/queries";
import type { ProduceStockAdjustmentResponse, ProduceStockOverviewItem } from "@/features/harvests/types";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { produceAdjustmentReasonLabels, qualityGradeLabels } from "@/lib/enum-labels";
import { qualityGrades, type QualityGrade, type SeasonStatus, type Unit } from "@/lib/enums";

interface SeasonStockGroup {
  seasonId: string;
  cropName: string;
  plotCode: string;
  seasonStartDate: string;
  seasonStatus: SeasonStatus;
  yieldUnit: Unit;
  items: ProduceStockOverviewItem[];
}

/** `/produce-stock`: xem tồn nông sản theo mùa vụ, loại chất lượng (FR-HAR-06) và lịch sử điều chỉnh của Chủ hộ (FR-HAR-07). */
export function ProduceStockPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: user } = useCurrentUser();
  const isOwner = user?.role === "Owner";
  const canSell = Boolean(user && canAccess(user, ACCESS.financeManager));

  const seasonId = searchParams.get("seasonId") ?? "";
  const rawGrade = searchParams.get("grade") ?? "";
  const qualityGrade: QualityGrade | undefined = (qualityGrades as readonly string[]).includes(rawGrade)
    ? (rawGrade as QualityGrade)
    : undefined;

  const [adjPage, setAdjPage] = useState(1);
  const [voidTarget, setVoidTarget] = useState<ProduceStockAdjustmentResponse | null>(null);
  const [voidError, setVoidError] = useState<unknown>(null);

  const stockQuery = useProduceStockOverview({
    seasonId: seasonId || undefined,
    qualityGrade,
  });

  const adjustmentsQuery = useProduceStockAdjustments(
    {
      seasonId: seasonId || undefined,
      qualityGrade,
      includeVoided: true,
      page: adjPage,
      pageSize: 10,
    },
    isOwner,
  );
  const voidAdjustment = useVoidProduceStockAdjustment();

  const updateFilters = useCallback(
    (nextSeasonId: string, nextGrade: string) => {
      const next = new URLSearchParams();
      if (nextSeasonId) {
        next.set("seasonId", nextSeasonId);
      }
      if (nextGrade) {
        next.set("grade", nextGrade);
      }
      setAdjPage(1);
      setSearchParams(next, { replace: true });
    },
    [setSearchParams],
  );

  const handleSeasonFilterChange = useCallback(
    (nextSeasonId: string) => {
      updateFilters(nextSeasonId, rawGrade);
    },
    [rawGrade, updateFilters],
  );

  const groups = useMemo<SeasonStockGroup[]>(() => {
    const items = stockQuery.data ?? [];
    const grouped = new Map<string, SeasonStockGroup>();
    for (const item of items) {
      const existing = grouped.get(item.seasonId);
      if (existing) {
        existing.items.push(item);
      } else {
        grouped.set(item.seasonId, {
          seasonId: item.seasonId,
          cropName: item.cropName,
          plotCode: item.plotCode,
          seasonStartDate: item.seasonStartDate,
          seasonStatus: item.seasonStatus,
          yieldUnit: item.yieldUnit,
          items: [item],
        });
      }
    }
    return [...grouped.values()];
  }, [stockQuery.data]);

  function openVoid(adjustment: ProduceStockAdjustmentResponse) {
    setVoidError(null);
    voidAdjustment.reset();
    setVoidTarget(adjustment);
  }

  async function confirmVoid(reason: string) {
    if (!voidTarget) {
      return;
    }
    setVoidError(null);
    try {
      await voidAdjustment.mutateAsync({ id: voidTarget.id, request: { reason } });
      showSuccess("Đã hủy điều chỉnh tồn nông sản.");
      setVoidTarget(null);
    } catch (error) {
      setVoidError(error);
    }
  }

  const isFiltered = Boolean(seasonId || qualityGrade);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Tồn nông sản"
        description="Sản lượng còn lại sau khi trừ lượng đã bán và điều chỉnh giảm."
        action={
          isOwner ? (
            <Button asChild size="sm">
              <Link to={seasonId ? `/produce-stock/adjust?seasonId=${seasonId}` : "/produce-stock/adjust"}>
                <SlidersHorizontalIcon className="mr-1.5 h-4 w-4" />
                Điều chỉnh tồn
              </Link>
            </Button>
          ) : undefined
        }
      />

      {/* Bộ lọc theo mùa vụ và loại chất lượng */}
      <div className="grid gap-4 rounded-xl border bg-card p-4 sm:grid-cols-2">
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="produce-stock-filter-season">Mùa vụ</Label>
          <SeasonSelect
            id="produce-stock-filter-season"
            value={seasonId}
            onChange={handleSeasonFilterChange}
            permission="ManageFinance"
            emptyLabel="Tất cả mùa vụ"
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <Label htmlFor="produce-stock-filter-grade">Loại chất lượng</Label>
          <NativeSelect
            id="produce-stock-filter-grade"
            value={qualityGrade ?? ""}
            onChange={(event) => updateFilters(seasonId, event.target.value)}
          >
            <NativeSelectOption value="">Tất cả loại chất lượng</NativeSelectOption>
            {qualityGrades.map((grade) => (
              <NativeSelectOption key={grade} value={grade}>
                {qualityGradeLabels[grade]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
      </div>

      {/* Danh sách tồn theo mùa vụ × loại */}
      {stockQuery.isPending ? (
        <LoadingState variant="list" />
      ) : stockQuery.isError ? (
        <ErrorAlert error={stockQuery.error} onRetry={() => void stockQuery.refetch()} />
      ) : groups.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => updateFilters("", "")} />
        ) : (
          <EmptyState message="Chưa có tồn nông sản nào." />
        )
      ) : (
        <div className="flex flex-col gap-4">
          {groups.map((group) => (
            <section key={group.seasonId} className="rounded-xl border bg-card">
              <div className="flex flex-wrap items-center justify-between gap-2 border-b px-4 py-3">
                <div className="min-w-0">
                  <Link
                    to={`/seasons/${group.seasonId}`}
                    className="font-semibold hover:underline"
                  >
                    {group.cropName} · {group.plotCode}
                  </Link>
                  <p className="text-xs text-muted-foreground">
                    Bắt đầu <DateText value={group.seasonStartDate} />
                  </p>
                </div>
                <StatusBadge kind="SeasonStatus" value={group.seasonStatus} />
              </div>

              <div className="divide-y">
                {group.items.map((item) => (
                  <div
                    key={item.qualityGrade}
                    className="flex flex-wrap items-center justify-between gap-3 px-4 py-3 text-sm"
                  >
                    <div className="flex flex-col gap-1">
                      <div className="flex items-center gap-2">
                        <StatusBadge kind="QualityGrade" value={item.qualityGrade} />
                        <span className="text-base font-semibold">
                          <QuantityText value={item.quantity} unit={item.yieldUnit} />
                        </span>
                      </div>
                      <p className="text-xs text-muted-foreground">
                        Thu <QuantityText value={item.totalHarvested} unit={item.yieldUnit} /> · Bán{" "}
                        <QuantityText value={item.totalSold} unit={item.yieldUnit} /> · Điều chỉnh{" "}
                        <QuantityText value={item.totalAdjusted} unit={item.yieldUnit} />
                      </p>
                    </div>

                    {item.quantity > 0 && item.seasonStatus === "InProgress" && (canSell || isOwner) && (
                      <div className="flex items-center gap-2">
                        {canSell && (
                          <Button size="sm" variant="outline" asChild>
                            <Link to={`/sales/new?seasonId=${item.seasonId}&grade=${item.qualityGrade}`}>
                              Bán
                            </Link>
                          </Button>
                        )}
                        {isOwner && (
                          <Button size="sm" variant="outline" asChild>
                            <Link to={`/produce-stock/adjust?seasonId=${item.seasonId}&grade=${item.qualityGrade}`}>
                              Điều chỉnh tồn
                            </Link>
                          </Button>
                        )}
                      </div>
                    )}
                  </div>
                ))}
              </div>
            </section>
          ))}
        </div>
      )}

      {/* Lịch sử điều chỉnh tồn: chỉ Chủ hộ thấy và hủy được điều chỉnh (FR-HAR-07) */}
      {isOwner && (
        <section className="flex flex-col gap-3">
          <h2 className="text-base font-semibold">Lịch sử điều chỉnh tồn nông sản</h2>
          {adjustmentsQuery.isPending ? (
            <LoadingState variant="list" />
          ) : adjustmentsQuery.isError ? (
            <ErrorAlert error={adjustmentsQuery.error} onRetry={() => void adjustmentsQuery.refetch()} />
          ) : adjustmentsQuery.data.items.length === 0 ? (
            <EmptyState message="Chưa có phiếu điều chỉnh tồn nông sản nào." />
          ) : (
            <>
              <div className="flex flex-col gap-2">
                {adjustmentsQuery.data.items.map((adj) => {
                  const reasonLabel =
                    adj.reason === "Other" && adj.reasonOther
                      ? `Khác: ${adj.reasonOther}`
                      : produceAdjustmentReasonLabels[adj.reason];
                  return (
                    <div
                      key={adj.id}
                      className="flex flex-wrap items-start justify-between gap-3 rounded-xl border bg-card p-4 text-sm"
                    >
                      <div className="flex min-w-0 flex-1 flex-col gap-1">
                        <div className="flex flex-wrap items-center gap-2">
                          <span className="font-medium">
                            {adj.cropName} · {adj.plotCode}
                          </span>
                          <StatusBadge kind="QualityGrade" value={adj.qualityGrade} />
                          {adj.voidedAt && <StatusBadge kind="Voided" />}
                        </div>
                        <p className="text-xs text-muted-foreground">
                          <DateText value={adj.adjustmentDate} /> · {reasonLabel}
                        </p>
                        {adj.note && <p className="text-xs text-muted-foreground">{adj.note}</p>}
                        {adj.voidReason && (
                          <p className="text-xs text-destructive">Lý do hủy: {adj.voidReason}</p>
                        )}
                      </div>

                      <div className="flex items-center gap-3">
                        <span className="font-semibold">
                          −<QuantityText value={adj.quantity} unit={adj.yieldUnit} />
                        </span>
                        {!adj.voidedAt && adj.seasonStatus === "InProgress" && (
                          <Button
                            size="sm"
                            variant="outline"
                            className="text-destructive hover:text-destructive"
                            onClick={() => openVoid(adj)}
                          >
                            <BanIcon className="mr-1.5 h-3.5 w-3.5" />
                            Hủy điều chỉnh
                          </Button>
                        )}
                      </div>
                    </div>
                  );
                })}
              </div>
              <Pagination meta={adjustmentsQuery.data.meta} onPageChange={setAdjPage} />
            </>
          )}
        </section>
      )}

      <ReasonDialog
        open={Boolean(voidTarget)}
        onOpenChange={(open) => !open && setVoidTarget(null)}
        title="Hủy điều chỉnh tồn nông sản"
        description={
          voidTarget
            ? `Hủy phiếu giảm ${voidTarget.quantity} (${qualityGradeLabels[voidTarget.qualityGrade]}) sẽ làm tồn nông sản của mùa vụ tăng lại tương ứng.`
            : undefined
        }
        confirmLabel="Hủy điều chỉnh"
        onConfirm={confirmVoid}
        pending={voidAdjustment.isPending}
        error={voidError}
        destructive
      />
    </div>
  );
}
