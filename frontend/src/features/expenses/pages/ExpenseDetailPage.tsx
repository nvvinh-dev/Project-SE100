import { BanIcon, EllipsisVerticalIcon, PencilIcon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Link, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
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
import { expenseTitle, seasonLabel } from "@/features/expenses/components/expense-labels";
import { useExpense, useVoidExpense } from "@/features/expenses/queries";
import { ApiError } from "@/lib/api-error";
import { formatDate, formatMoney } from "@/lib/format";

/** `/expenses/:id`: chi tiết khoản chi; điều chỉnh, hủy qua menu ⋯ (Chủ hộ, TV+TC). */
export function ExpenseDetailPage() {
  const { id = "" } = useParams();
  const expense = useExpense(id);
  const voidExpense = useVoidExpense(id);
  const [voidOpen, setVoidOpen] = useState(false);
  const [voidError, setVoidError] = useState<unknown>(null);

  if (expense.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (expense.isError) {
    // BR-GEN-02: khoản chi của hộ khác cũng trả NOT_FOUND, hiện như không tồn tại.
    return expense.error instanceof ApiError && expense.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={expense.error} onRetry={() => void expense.refetch()} />
    );
  }

  const data = expense.data;
  const isVoided = data.voidedAt !== null;
  // BR-EXP-02: khoản chi gắn mùa vụ chỉ điều chỉnh, hủy được khi mùa vụ đang canh tác; không được thì ẩn thao tác.
  const canChange = !isVoided && (data.seasonId === null || data.seasonStatus === "InProgress");

  function openVoid() {
    setVoidError(null);
    voidExpense.reset();
    setVoidOpen(true);
  }

  async function confirmVoid(reason: string) {
    setVoidError(null);
    try {
      await voidExpense.mutateAsync({ reason });
      showSuccess("Đã hủy khoản chi.");
      setVoidOpen(false);
    } catch (error) {
      // Lỗi hiện trong hộp thoại, giữ nguyên lý do đã nhập.
      setVoidError(error);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={expenseTitle(data)}
        description={<MoneyText amount={data.amount} />}
        backTo="/expenses"
        action={
          canChange && (
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                  <EllipsisVerticalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem asChild className="min-h-11">
                  <Link to={`/expenses/${data.id}/correct`}>
                    <PencilIcon />
                    Điều chỉnh chi phí
                  </Link>
                </DropdownMenuItem>
                <DropdownMenuSeparator />
                {/* Thao tác làm mất dữ liệu để cuối menu, chữ đỏ. */}
                <DropdownMenuItem variant="destructive" className="min-h-11" onSelect={openVoid}>
                  <BanIcon />
                  Hủy khoản chi
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
          <InfoRow label="Số tiền">
            <MoneyText amount={data.amount} />
          </InfoRow>
          <InfoRow label="Ngày phát sinh">
            <DateText value={data.expenseDate} />
          </InfoRow>
          <InfoRow label="Mùa vụ">
            {data.seasonId ? (
              <Link to={`/seasons/${data.seasonId}`} className="text-primary underline-offset-4 hover:underline">
                {seasonLabel(data)}
              </Link>
            ) : (
              "Chi phí chung của hộ"
            )}
          </InfoRow>
          <InfoRow label="Nhóm">{expenseTitle(data)}</InfoRow>
          <InfoRow label="Ghi chú" wide>
            {data.note ?? "—"}
          </InfoRow>
        </dl>
      </section>

      <ReasonDialog
        open={voidOpen}
        onOpenChange={setVoidOpen}
        title="Hủy khoản chi"
        description={`Khoản chi ${formatMoney(data.amount)} ngày ${formatDate(data.expenseDate)} sẽ không còn tính vào chi phí. Không hoàn tác được.`}
        confirmLabel="Hủy khoản chi"
        onConfirm={(reason) => void confirmVoid(reason)}
        pending={voidExpense.isPending}
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
