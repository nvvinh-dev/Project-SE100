import { BanIcon, EllipsisVerticalIcon, PencilIcon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Link, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useHarvest, useVoidHarvest } from "@/features/harvests/queries";
import { ApiError } from "@/lib/api-error";
import { qualityGradeLabels, salesChannelLabels } from "@/lib/enum-labels";
import { formatDate, formatQuantity } from "@/lib/format";

/** `/harvests/:id`: chi tiết phiếu thu hoạch kèm thao tác đính chính, hủy phiếu (FR-HAR-03, FR-HAR-04). */
export function HarvestDetailPage() {
  const { id = "" } = useParams();
  const harvest = useHarvest(id);
  const voidHarvest = useVoidHarvest(id);
  const [voidOpen, setVoidOpen] = useState(false);
  const [voidError, setVoidError] = useState<unknown>(null);

  if (harvest.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (harvest.isError) {
    return harvest.error instanceof ApiError && harvest.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={harvest.error} onRetry={() => void harvest.refetch()} />
    );
  }

  const data = harvest.data;
  const isVoided = Boolean(data.voidedAt);

  function openVoid() {
    setVoidError(null);
    voidHarvest.reset();
    setVoidOpen(true);
  }

  async function confirmVoid(reason: string) {
    setVoidError(null);
    try {
      await voidHarvest.mutateAsync({ reason });
      showSuccess("Đã hủy phiếu thu hoạch.");
      setVoidOpen(false);
    } catch (error) {
      setVoidError(error);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={`${data.cropName} · ${data.plotCode}`}
        description={
          <span className="inline-flex items-center gap-2">
            <QuantityText value={data.quantity} unit={data.yieldUnit} />
            <span>·</span>
            <span>{qualityGradeLabels[data.qualityGrade]}</span>
          </span>
        }
        backTo="/harvests"
        action={
          data.canModify && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                  <EllipsisVerticalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem asChild className="min-h-11">
                  <Link to={`/harvests/${data.id}/correct`}>
                    <PencilIcon />
                    Điều chỉnh phiếu
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                <DropdownMenuItem variant="destructive" className="min-h-11" onSelect={openVoid}>
                  <BanIcon />
                  Hủy phiếu thu
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )
        }
      />

      <section className="flex flex-col gap-4 rounded-lg border p-4">
        {isVoided && (
          <div className="flex flex-col gap-1">
            <div>
              <StatusBadge kind="Voided" />
            </div>
            {data.voidReason && <p className="text-sm text-muted-foreground">Lý do hủy: {data.voidReason}</p>}
          </div>
        )}

        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Sản lượng thu">
            <span className="font-semibold">
              <QuantityText value={data.quantity} unit={data.yieldUnit} />
            </span>
          </InfoRow>
          <InfoRow label="Loại chất lượng">
            <StatusBadge kind="QualityGrade" value={data.qualityGrade} />
          </InfoRow>
          <InfoRow label="Ngày thu hoạch">
            <DateText value={data.harvestDate} />
          </InfoRow>
          <InfoRow label="Mùa vụ">
            <Link to={`/seasons/${data.seasonId}`} className="text-primary underline-offset-4 hover:underline">
              {data.cropName} · {data.plotCode} · {formatDate(data.seasonStartDate)}
            </Link>
          </InfoRow>
          <InfoRow label="Ghi chú" wide>
            {data.note ?? "—"}
          </InfoRow>
        </dl>
      </section>

      {data.linkedSales.length > 0 && (
        <section className="flex flex-col gap-3 rounded-lg border p-4">
          <h2 className="text-sm font-semibold">Giao dịch bán đang liên kết</h2>
          <ul className="divide-y">
            {data.linkedSales.map((sale) => (
              <li key={sale.saleId} className="flex items-center justify-between gap-2 py-2 text-sm">
                <div className="flex flex-col">
                  <span className="font-medium">
                    {sale.customerName ?? (sale.channel ? salesChannelLabels[sale.channel] : "Bán lẻ")}
                  </span>
                  <span className="text-xs text-muted-foreground">
                    <DateText value={sale.saleDate} />
                  </span>
                </div>
                <QuantityText value={sale.quantity} unit={data.yieldUnit} />
              </li>
            ))}
          </ul>
        </section>
      )}

      <ReasonDialog
        open={voidOpen}
        onOpenChange={setVoidOpen}
        title="Hủy phiếu thu hoạch"
        description={`Phiếu thu ${formatQuantity(data.quantity, data.yieldUnit)} (${qualityGradeLabels[data.qualityGrade]}) ngày ${formatDate(data.harvestDate)} sẽ bị hủy khỏi tổng sản lượng và tồn nông sản.`}
        confirmLabel="Hủy phiếu thu"
        onConfirm={(reason) => void confirmVoid(reason)}
        pending={voidHarvest.isPending}
        error={voidError}
        destructive
      />
    </div>
  );
}

function InfoRow({ label, wide = false, children }: { label: string; wide?: boolean; children: ReactNode }) {
  return (
    <div className={wide ? "sm:col-span-2" : undefined}>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words whitespace-pre-line">{children}</dd>
    </div>
  );
}
