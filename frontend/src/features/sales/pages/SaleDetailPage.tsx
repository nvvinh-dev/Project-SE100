import type { ReactNode } from "react";
import { Link, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buyerLabel, seasonShortLabel } from "@/features/sales/components/sale-labels";
import { useSale } from "@/features/sales/queries";
import type { SaleResponse } from "@/features/sales/types";
import { ApiError } from "@/lib/api-error";
import { qualityGradeLabels, unitLabels } from "@/lib/enum-labels";
import { formatMoney } from "@/lib/format";

/** `/sales/:id`: chi tiết giao dịch bán (Chủ hộ, TV+TC). */
export function SaleDetailPage() {
  const { id = "" } = useParams();
  const sale = useSale(id);

  if (sale.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (sale.isError) {
    // BR-GEN-02: giao dịch của hộ khác cũng trả NOT_FOUND, hiện như không tồn tại.
    return sale.error instanceof ApiError && sale.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={sale.error} onRetry={() => void sale.refetch()} />
    );
  }

  const data = sale.data;
  const isVoided = data.voidedAt !== null;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={`Bán ${seasonShortLabel(data)}`}
        description={<MoneyText amount={data.revenue} />}
        backTo="/sales"
      />

      <section className="flex flex-col gap-4 rounded-lg border p-4">
        <div className="flex flex-col gap-1">
          <div>
            {isVoided ? <StatusBadge kind="Voided" /> : <StatusBadge kind="PaymentStatus" value={data.paymentStatus} />}
          </div>
          {isVoided && data.voidReason && <p className="text-sm text-muted-foreground">Lý do hủy: {data.voidReason}</p>}
        </div>
        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Mùa vụ">
            <Link to={`/seasons/${data.seasonId}`} className="text-primary underline-offset-4 hover:underline">
              {seasonShortLabel(data)}
            </Link>
          </InfoRow>
          <InfoRow label="Khách hàng hoặc kênh">{buyerLabel(data)}</InfoRow>
          <InfoRow label="Loại chất lượng">{qualityGradeLabels[data.qualityGrade]}</InfoRow>
          <InfoRow label="Ngày bán">
            <DateText value={data.saleDate} />
          </InfoRow>
          <InfoRow label="Số lượng">
            <QuantityText value={data.quantity} unit={data.yieldUnit} />
          </InfoRow>
          <InfoRow label="Đơn giá">{`${formatMoney(data.unitPrice)}/${unitLabels[data.yieldUnit]}`}</InfoRow>
          <InfoRow label="Doanh thu">
            <MoneyText amount={data.revenue} />
          </InfoRow>
          <InfoRow label="Đã thu">
            <MoneyText amount={data.paidAmount} />
          </InfoRow>
          <InfoRow label="Còn nợ">
            <MoneyText amount={data.outstandingAmount} />
          </InfoRow>
          <InfoRow label="Ghi chú" wide>
            {data.note ?? "—"}
          </InfoRow>
        </dl>
      </section>

      <HarvestLinksSection sale={data} />
    </div>
  );
}

/** Khối "Phiếu thu đã liên kết" (FR-SAL-03): ngày thu, sản lượng. */
function HarvestLinksSection({ sale }: { sale: SaleResponse }) {
  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-base font-semibold">Phiếu thu đã liên kết</h2>
      {sale.harvestLinks.length === 0 ? (
        <p className="text-sm text-muted-foreground">Không liên kết phiếu thu nào.</p>
      ) : (
        <ul className="flex flex-col divide-y rounded-lg border">
          {sale.harvestLinks.map((link) => (
            <li key={link.harvestRecordId} className="flex min-h-11 items-center justify-between gap-3 px-4 py-2">
              <DateText value={link.harvestDate} />
              <QuantityText value={link.quantity} unit={sale.yieldUnit} />
            </li>
          ))}
        </ul>
      )}
    </section>
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
