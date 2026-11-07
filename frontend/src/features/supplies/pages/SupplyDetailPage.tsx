import { ArrowUpDownIcon, EllipsisVerticalIcon, PackagePlusIcon, PencilIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Link, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { PermissionGate } from "@/components/common/PermissionGate";
import { QuantityText } from "@/components/common/QuantityText";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { StockLevelBadge } from "@/features/supplies/components/StockLevelBadge";
import { useSupply } from "@/features/supplies/queries";
import { ApiError } from "@/lib/api-error";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { supplyGroupLabels } from "@/lib/enum-labels";

/** `/supplies/:id`: chi tiết vật tư. Lịch sử giao dịch của vật tư thêm ở FR-SUP-08. */
export function SupplyDetailPage() {
  const { id = "" } = useParams();
  const supply = useSupply(id);
  const { data: user } = useCurrentUser();
  const canEdit = user !== undefined && canAccess(user, ACCESS.owner);
  const canSeePrices = user !== undefined && canAccess(user, ACCESS.stockManager);

  if (supply.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (supply.isError) {
    // BR-GEN-02: vật tư của hộ khác cũng trả NOT_FOUND, hiện như không tồn tại.
    return supply.error instanceof ApiError && supply.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={supply.error} onRetry={() => void supply.refetch()} />
    );
  }

  const data = supply.data;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={data.name}
        description={data.code}
        backTo="/supplies"
        action={
          // Thao tác phụ nằm trong menu ⋯; thao tác không được phép thì ẩn hẳn.
          <PermissionGate allowed={canEdit || canSeePrices}>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                  <EllipsisVerticalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                {canSeePrices && (
                  <>
                    <DropdownMenuItem asChild className="min-h-11">
                      <Link to={`/stock/in?supplyId=${data.id}`}>
                        <PackagePlusIcon />
                        Nhập kho
                      </Link>
                    </DropdownMenuItem>
                    <DropdownMenuItem asChild className="min-h-11">
                      <Link to={`/stock/adjust?supplyId=${data.id}`}>
                        <ArrowUpDownIcon />
                        Điều chỉnh tồn
                      </Link>
                    </DropdownMenuItem>
                  </>
                )}
                {canEdit && (
                  <DropdownMenuItem asChild className="min-h-11">
                    <Link to={`/supplies/${data.id}/edit`}>
                      <PencilIcon />
                      Sửa vật tư
                    </Link>
                  </DropdownMenuItem>
                )}
              </DropdownMenuContent>
            </DropdownMenu>
          </PermissionGate>
        }
      />

      <section className="flex flex-col gap-4 rounded-lg border p-4">
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge kind="RecordStatus" value={data.status} />
          <StockLevelBadge stockQuantity={data.stockQuantity} minStockLevel={data.minStockLevel} />
        </div>
        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Tồn hiện tại">
            <QuantityText value={data.stockQuantity} unit={data.unit} />
          </InfoRow>
          <InfoRow label="Ngưỡng tồn tối thiểu">
            <QuantityText value={data.minStockLevel} unit={data.unit} />
          </InfoRow>
          <InfoRow label="Nhóm">{supplyGroupLabels[data.group]}</InfoRow>
          {canSeePrices && (
            <>
              <InfoRow label="Giá nhập tham khảo">
                {data.referencePrice === undefined ? "—" : <MoneyText amount={data.referencePrice} />}
              </InfoRow>
              <InfoRow label="Giá vốn bình quân">
                {data.averageUnitCost === undefined ? "—" : <MoneyText amount={data.averageUnitCost} />}
              </InfoRow>
              <InfoRow label="Giá trị kho">
                {data.averageUnitCost === undefined ? (
                  "—"
                ) : (
                  <MoneyText amount={data.stockQuantity * data.averageUnitCost} />
                )}
              </InfoRow>
            </>
          )}
          <InfoRow label="Ghi chú" wide>
            {data.note ?? "—"}
          </InfoRow>
        </dl>
      </section>
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
