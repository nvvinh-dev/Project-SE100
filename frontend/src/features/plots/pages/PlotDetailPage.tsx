import { EllipsisVerticalIcon, PencilIcon } from "lucide-react";
import type { ReactNode } from "react";
import { Link, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { PermissionGate } from "@/components/common/PermissionGate";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { usePlot } from "@/features/plots/queries";
import type { PlotResponse } from "@/features/plots/types";
import { ApiError } from "@/lib/api-error";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { soilTypeLabels, waterSourceLabels } from "@/lib/enum-labels";
import { formatArea } from "@/lib/format";

/** `/plots/:id`: chi tiết thửa đất (mẫu trang chi tiết). Danh sách mùa vụ trên thửa thêm ở FR-PLOT-04. */
export function PlotDetailPage() {
  const { id = "" } = useParams();
  const plot = usePlot(id);
  const { data: user } = useCurrentUser();
  const canEdit = user !== undefined && canAccess(user, ACCESS.owner);

  if (plot.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (plot.isError) {
    // BR-GEN-02: thửa của hộ khác cũng trả NOT_FOUND, hiện như không tồn tại.
    return plot.error instanceof ApiError && plot.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={plot.error} onRetry={() => void plot.refetch()} />
    );
  }

  const data = plot.data;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={data.code}
        description={data.locationDescription ?? undefined}
        backTo="/plots"
        action={
          // Thao tác phụ nằm trong menu ⋯; thao tác không được phép thì ẩn hẳn.
          <PermissionGate allowed={canEdit}>
            <DropdownMenu>
              <DropdownMenuTrigger asChild>
                <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                  <EllipsisVerticalIcon />
                </Button>
              </DropdownMenuTrigger>
              <DropdownMenuContent align="end">
                <DropdownMenuItem asChild className="min-h-11">
                  <Link to={`/plots/${data.id}/edit`}>
                    <PencilIcon />
                    Sửa thửa đất
                  </Link>
                </DropdownMenuItem>
              </DropdownMenuContent>
            </DropdownMenu>
          </PermissionGate>
        }
      />

      <section className="flex flex-col gap-4 rounded-lg border p-4">
        {data.status && (
          <div>
            <StatusBadge kind="RecordStatus" value={data.status} />
          </div>
        )}
        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Diện tích">{data.areaM2 === null ? "—" : formatArea(data.areaM2)}</InfoRow>
          <InfoRow label="Vị trí">{data.locationDescription ?? "—"}</InfoRow>
          <InfoRow label="Loại đất">{soilTypeText(data)}</InfoRow>
          <InfoRow label="Nguồn nước">{waterSourceText(data)}</InfoRow>
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

// Chọn "Khác" thì hiện kèm ghi chú tự do người dùng đã nhập.
function soilTypeText(plot: PlotResponse): string {
  if (plot.soilType === null) {
    return "—";
  }
  return plot.soilType === "Other" && plot.soilTypeOther
    ? `${soilTypeLabels.Other}: ${plot.soilTypeOther}`
    : soilTypeLabels[plot.soilType];
}

function waterSourceText(plot: PlotResponse): string {
  if (plot.waterSource === null) {
    return "—";
  }
  return plot.waterSource === "Other" && plot.waterSourceOther
    ? `${waterSourceLabels.Other}: ${plot.waterSourceOther}`
    : waterSourceLabels[plot.waterSource];
}
