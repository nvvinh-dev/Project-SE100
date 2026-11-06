import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { PlotForm, type PlotFormSubmit } from "@/features/plots/components/PlotForm";
import { usePlot, useUpdatePlot } from "@/features/plots/queries";
import { plotToFormValues, setPlotFieldErrors, toUpdatePlotRequest } from "@/features/plots/schemas";
import { ApiError } from "@/lib/api-error";

/** `/plots/:id/edit`: sửa thửa đất, kể cả đổi tình trạng sử dụng (Chủ hộ). */
export function PlotEditPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const plot = usePlot(id);
  const updatePlot = useUpdatePlot(id);
  // Người khác đã sửa thửa sau khi mở form (BR-GEN-11): báo và cho tải lại bản mới.
  // Ghi kèm mã thửa để không hiện nhầm sang thửa khác khi đổi :id mà trang không dựng lại.
  const [conflict, setConflict] = useState<{ plotId: string; error: unknown } | null>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/plots/${id}`;

  if (plot.isPending) {
    return <LoadingState variant="form" />;
  }

  if (plot.isError) {
    return isNotFound(plot.error) ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={plot.error} onRetry={() => void plot.refetch()} />
    );
  }

  const handleSubmit: PlotFormSubmit = async (values, setError) => {
    setConflict(null);
    try {
      const saved = await updatePlot.mutateAsync(toUpdatePlotRequest(values, plot.data.version ?? 0));
      showSuccess(`Đã lưu thửa đất ${saved.code}.`);
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict({ plotId: id, error });
        return;
      }
      if (!setPlotFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  async function reload() {
    await plot.refetch();
    setConflict(null);
    // Đổi key để form dựng lại với giá trị và version mới; dữ liệu đang nhập dở bị thay bằng bản mới.
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={`Sửa thửa đất ${plot.data.code}`} backTo={backTo} />
      <PlotForm
        // Dựng lại form khi đổi sang thửa khác (cùng route, khác :id) hoặc sau khi tải lại bản mới.
        key={`${id}:${formKey}`}
        mode="edit"
        defaultValues={plotToFormValues(plot.data)}
        onSubmit={handleSubmit}
        pending={updatePlot.isPending}
        backTo={backTo}
        notice={
          conflict?.plotId === id && (
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
