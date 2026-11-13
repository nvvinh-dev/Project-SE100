import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { SeasonForm, type SeasonFormSubmit } from "@/features/seasons/components/SeasonForm";
import { useSeason, useUpdateSeason } from "@/features/seasons/queries";
import {
  seasonToFormValues,
  setSeasonFieldErrors,
  toUpdateSeasonRequest,
} from "@/features/seasons/schemas";
import { ApiError } from "@/lib/api-error";

/** `/seasons/:id/edit`: sửa thông tin mùa vụ (Chủ hộ, FR-SEA-07). */
export function SeasonEditPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const season = useSeason(id);
  const updateSeason = useUpdateSeason(id);

  // BR-GEN-11: Xử lý xung đột phiên bản khi người khác đã cập nhật trước
  const [conflict, setConflict] = useState<{ seasonId: string; error: unknown } | null>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/seasons/${id}`;

  if (season.isPending) {
    return <LoadingState variant="form" />;
  }

  if (season.isError) {
    return isNotFound(season.error) ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={season.error} onRetry={() => void season.refetch()} />
    );
  }

  const isTerminated = season.data.status === "Closed" || season.data.status === "Cancelled";
  if (isTerminated) {
    const statusLabel = season.data.status === "Closed" ? "đã chốt" : "đã hủy";
    return (
      <div className="flex flex-col gap-6">
        <PageHeader
          title={`Sửa mùa vụ ${season.data.cropName} · ${season.data.plotCode}`}
          backTo={backTo}
        />
        <Alert>
          <AlertDescription>
            Mùa vụ {statusLabel} không thể sửa thông tin.
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  const handleSubmit: SeasonFormSubmit = async (values, setError) => {
    setConflict(null);
    try {
      const saved = await updateSeason.mutateAsync(
        toUpdateSeasonRequest(values, season.data.version ?? 0),
      );
      showSuccess(`Đã lưu mùa vụ ${saved.cropName} · ${saved.plotCode}.`);
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict({ seasonId: id, error });
        return;
      }
      if (!setSeasonFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  async function reload() {
    await season.refetch();
    setConflict(null);
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={`Sửa mùa vụ ${season.data.cropName} · ${season.data.plotCode}`}
        backTo={backTo}
      />
      <SeasonForm
        key={`${id}:${formKey}`}
        mode="edit"
        defaultValues={seasonToFormValues(season.data)}
        onSubmit={handleSubmit}
        pending={updateSeason.isPending}
        backTo={backTo}
        seasonStatus={season.data.status}
        selectedCropFallback={{
          id: season.data.cropId,
          name: season.data.cropName,
          yieldUnit: season.data.yieldUnit,
        }}
        notice={
          conflict?.seasonId === id && (
            <ErrorAlert error={conflict.error} onRetry={() => void reload()} retryLabel="Tải lại" />
          )
        }
      />
    </div>
  );
}

function isNotFound(error: unknown): boolean {
  return error instanceof ApiError && error.code === "NOT_FOUND";
}
