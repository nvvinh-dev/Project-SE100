import { useState } from "react";
import type { UseFormSetError } from "react-hook-form";
import { Navigate, useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { showSuccess } from "@/components/common/toast";
import { HarvestForm, type HarvestFormSubmit } from "@/features/harvests/components/HarvestForm";
import { useCorrectHarvest, useHarvest } from "@/features/harvests/queries";
import {
  type HarvestFormOutput,
  type HarvestFormValues,
  harvestFormFields,
  harvestToFormValues,
  toCorrectHarvestRequest,
} from "@/features/harvests/schemas";
import { ApiError } from "@/lib/api-error";
import { setFieldErrors } from "@/lib/form-errors";
import { formatDate } from "@/lib/format";

interface PendingCorrection {
  values: HarvestFormOutput;
  setError: UseFormSetError<HarvestFormValues>;
}

/** `/harvests/:id/correct`: đính chính phiếu thu hoạch ghi nhầm (FR-HAR-03, BR-GEN-06, BR-GEN-11). */
export function HarvestCorrectPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const harvest = useHarvest(id);
  const correctHarvest = useCorrectHarvest(id);
  const [pendingCorrection, setPendingCorrection] = useState<PendingCorrection | null>(null);
  const [dialogError, setDialogError] = useState<unknown>(null);
  const [conflict, setConflict] = useState<unknown>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/harvests/${id}`;

  if (harvest.isPending) {
    return <LoadingState variant="form" />;
  }

  if (harvest.isError) {
    return harvest.error instanceof ApiError && harvest.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={harvest.error} onRetry={() => void harvest.refetch()} />
    );
  }

  if (harvest.data.voidedAt || !harvest.data.canModify) {
    return <Navigate to={backTo} replace />;
  }

  const handleSubmit: HarvestFormSubmit = async (values, setError) => {
    setConflict(null);
    setDialogError(null);
    correctHarvest.reset();
    setPendingCorrection({ values, setError });
  };

  async function confirm(reason: string) {
    if (!pendingCorrection || !harvest.data) {
      return;
    }
    try {
      await correctHarvest.mutateAsync(toCorrectHarvestRequest(pendingCorrection.values, reason, harvest.data.version));
      showSuccess("Đã điều chỉnh phiếu thu hoạch.");
      setPendingCorrection(null);
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict(error);
        setPendingCorrection(null);
        return;
      }
      if (setFieldErrors(error, pendingCorrection.setError, harvestFormFields)) {
        setPendingCorrection(null);
        return;
      }
      setDialogError(error);
    }
  }

  async function reload() {
    await harvest.refetch();
    setConflict(null);
    setFormKey((key) => key + 1);
  }

  const seasonLabel = `${harvest.data.cropName} · ${harvest.data.plotCode} · ${formatDate(harvest.data.seasonStartDate)}`;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Điều chỉnh phiếu thu hoạch" backTo={backTo} />
      <HarvestForm
        key={`${id}:${formKey}`}
        mode="correct"
        defaultValues={harvestToFormValues(harvest.data)}
        fixedSeasonInfo={{
          label: seasonLabel,
          yieldUnit: harvest.data.yieldUnit,
          startDate: harvest.data.seasonStartDate,
        }}
        onSubmit={handleSubmit}
        pending={correctHarvest.isPending}
        backTo={backTo}
        submitLabel="Lưu điều chỉnh"
        notice={conflict !== null && <ErrorAlert error={conflict} onRetry={() => void reload()} retryLabel="Tải lại" />}
      />
      <ReasonDialog
        open={pendingCorrection !== null}
        onOpenChange={(open) => !open && setPendingCorrection(null)}
        title="Điều chỉnh phiếu thu hoạch"
        description="Giá trị cũ và mới sẽ được lưu vào nhật ký kèm lý do."
        confirmLabel="Xác nhận điều chỉnh"
        onConfirm={(reason) => void confirm(reason)}
        pending={correctHarvest.isPending}
        error={dialogError}
      />
    </div>
  );
}
