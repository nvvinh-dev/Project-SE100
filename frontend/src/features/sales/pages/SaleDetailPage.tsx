import { BanIcon, EllipsisVerticalIcon, HandCoinsIcon, PencilIcon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Link, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
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
import { PaymentDialog } from "@/features/sales/components/PaymentDialog";
import { buyerLabel, seasonShortLabel } from "@/features/sales/components/sale-labels";
import { useSale, useVoidPayment, useVoidSale } from "@/features/sales/queries";
import type { PaymentResponse, SaleResponse } from "@/features/sales/types";
import { ApiError } from "@/lib/api-error";
import { qualityGradeLabels, unitLabels } from "@/lib/enum-labels";
import { formatDate, formatMoney } from "@/lib/format";

/**
 * `/sales/:id`: chi tiết giao dịch bán (Chủ hộ, TV+TC). Nút chính "Ghi thanh toán" khi còn nợ; điều chỉnh, hủy giao dịch
 * qua menu ⋯; mỗi lần thanh toán có "Hủy thanh toán".
 */
export function SaleDetailPage() {
  const { id = "" } = useParams();
  const sale = useSale(id);
  const voidSale = useVoidSale(id);
  const voidPayment = useVoidPayment();
  const [paymentOpen, setPaymentOpen] = useState(false);
  const [voidSaleOpen, setVoidSaleOpen] = useState(false);
  const [voidSaleError, setVoidSaleError] = useState<unknown>(null);
  const [paymentToVoid, setPaymentToVoid] = useState<PaymentResponse | null>(null);
  const [voidPaymentError, setVoidPaymentError] = useState<unknown>(null);

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
  // Mục 4.3: điều chỉnh, hủy giao dịch khi mùa vụ đang canh tác; ghi, hủy thanh toán cả khi mùa vụ đã chốt.
  const canChangeSale = !isVoided && data.seasonStatus === "InProgress";
  const canChangePayments = !isVoided && (data.seasonStatus === "InProgress" || data.seasonStatus === "Closed");
  const canRecordPayment = canChangePayments && data.outstandingAmount > 0;

  function openVoidSale() {
    setVoidSaleError(null);
    voidSale.reset();
    setVoidSaleOpen(true);
  }

  async function confirmVoidSale(reason: string) {
    setVoidSaleError(null);
    try {
      await voidSale.mutateAsync({ reason });
      showSuccess("Đã hủy giao dịch bán.");
      setVoidSaleOpen(false);
    } catch (error) {
      // SALE_HAS_PAYMENTS và lỗi khác hiện nguyên văn trong hộp thoại, giữ lý do đã nhập.
      setVoidSaleError(error);
    }
  }

  function openVoidPayment(payment: PaymentResponse) {
    setVoidPaymentError(null);
    voidPayment.reset();
    setPaymentToVoid(payment);
  }

  async function confirmVoidPayment(reason: string) {
    if (!paymentToVoid) {
      return;
    }
    setVoidPaymentError(null);
    try {
      await voidPayment.mutateAsync({ id: paymentToVoid.id, reason });
      showSuccess("Đã hủy thanh toán.");
      setPaymentToVoid(null);
    } catch (error) {
      setVoidPaymentError(error);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={`Bán ${seasonShortLabel(data)}`}
        description={<MoneyText amount={data.revenue} />}
        backTo="/sales"
        action={
          canChangeSale && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                  <EllipsisVerticalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem asChild className="min-h-11">
                  <Link to={`/sales/${data.id}/correct`}>
                    <PencilIcon />
                    Điều chỉnh giao dịch
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/* Thao tác làm mất dữ liệu để cuối menu, chữ đỏ. */}
                <DropdownMenuItem variant="destructive" className="min-h-11" onSelect={openVoidSale}>
                  <BanIcon />
                  Hủy giao dịch
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          )
        }
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
            <MoneyText amount={data.outstandingAmount} className="font-semibold" />
          </InfoRow>
          <InfoRow label="Ghi chú" wide>
            {data.note ?? "—"}
          </InfoRow>
        </dl>
        {canRecordPayment && (
          <Button className="min-h-11 sm:self-start" onClick={() => setPaymentOpen(true)}>
            <HandCoinsIcon data-icon="inline-start" />
            Ghi thanh toán
          </Button>
        )}
      </section>

      <PaymentsSection sale={data} canVoid={canChangePayments} onVoid={openVoidPayment} />
      <HarvestLinksSection sale={data} />

      <PaymentDialog
        open={paymentOpen}
        onOpenChange={setPaymentOpen}
        saleId={data.id}
        outstanding={data.outstandingAmount}
      />
      <ReasonDialog
        open={voidSaleOpen}
        onOpenChange={setVoidSaleOpen}
        title="Hủy giao dịch"
        description={`Giao dịch bán ${formatMoney(data.revenue)} ngày ${formatDate(data.saleDate)} sẽ không còn tính vào doanh thu, tồn nông sản được trả lại. Không hoàn tác được.`}
        confirmLabel="Hủy giao dịch"
        onConfirm={(reason) => void confirmVoidSale(reason)}
        pending={voidSale.isPending}
        error={voidSaleError}
        destructive
      />
      <ReasonDialog
        open={paymentToVoid !== null}
        onOpenChange={(open) => !open && setPaymentToVoid(null)}
        title="Hủy thanh toán"
        description={
          paymentToVoid &&
          `Lần thanh toán ${formatMoney(paymentToVoid.amount)} ngày ${formatDate(paymentToVoid.paymentDate)} sẽ không còn tính vào đã thu, công nợ tăng lại. Không hoàn tác được.`
        }
        confirmLabel="Hủy thanh toán"
        onConfirm={(reason) => void confirmVoidPayment(reason)}
        pending={voidPayment.isPending}
        error={voidPaymentError}
        destructive
      />
    </div>
  );
}

/** Các lần thanh toán, mới nhất trước; lần đã hủy hiện nhãn "Đã hủy" và lý do. */
function PaymentsSection({
  sale,
  canVoid,
  onVoid,
}: {
  sale: SaleResponse;
  canVoid: boolean;
  onVoid: (payment: PaymentResponse) => void;
}) {
  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-base font-semibold">Các lần thanh toán</h2>
      {sale.payments.length === 0 ? (
        <p className="text-sm text-muted-foreground">Chưa có lần thanh toán nào.</p>
      ) : (
        <ul className="flex flex-col divide-y rounded-lg border">
          {sale.payments.map((payment) => (
            <li key={payment.id} className="flex flex-wrap items-center justify-between gap-3 px-4 py-3">
              <div className="flex flex-col gap-0.5">
                <span className={payment.voidedAt ? "line-through" : "font-medium"}>
                  <MoneyText amount={payment.amount} />
                </span>
                <span className="text-sm text-muted-foreground">
                  <DateText value={payment.paymentDate} />
                  {payment.note && ` · ${payment.note}`}
                </span>
                {payment.voidedAt && payment.voidReason && (
                  <span className="text-sm text-muted-foreground">Lý do hủy: {payment.voidReason}</span>
                )}
              </div>
              {payment.voidedAt ? (
                <StatusBadge kind="Voided" />
              ) : (
                canVoid && (
                  <Button variant="outline" className="min-h-11" onClick={() => onVoid(payment)}>
                    Hủy thanh toán
                  </Button>
                )
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

/** Khối "Phiếu thu hoạch đã liên kết" (FR-SAL-03): ngày thu, sản lượng. */
function HarvestLinksSection({ sale }: { sale: SaleResponse }) {
  return (
    <section className="flex flex-col gap-3">
      <h2 className="text-base font-semibold">Phiếu thu hoạch đã liên kết</h2>
      {sale.harvestLinks.length === 0 ? (
        <p className="text-sm text-muted-foreground">Không liên kết phiếu thu hoạch nào.</p>
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
