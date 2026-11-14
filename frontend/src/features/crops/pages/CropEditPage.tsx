import { useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { CropForm, type CropFormSubmit } from "@/features/crops/components/CropForm";
import { useCrop, useUpdateCrop } from "@/features/crops/queries";
import { cropToFormValues, setCropFieldErrors, toUpdateCropRequest } from "@/features/crops/schemas";
import { ApiError } from "@/lib/api-error";

/** `/crops/:id/edit`: sửa cây trồng, kể cả đổi trạng thái (Chủ hộ). */
export function CropEditPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const crop = useCrop(id);
  const updateCrop = useUpdateCrop(id);
  // Người khác đã sửa cây sau khi mở form (BR-GEN-11): báo và cho tải lại bản mới.
  // Ghi kèm id cây để không hiện nhầm sang cây khác khi đổi :id mà trang không dựng lại.
  const [conflict, setConflict] = useState<{ cropId: string; error: unknown } | null>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = "/crops";

  if (crop.isPending) {
    return <LoadingState variant="form" />;
  }

  if (crop.isError) {
    return isNotFound(crop.error) ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={crop.error} onRetry={() => void crop.refetch()} />
    );
  }

  const handleSubmit: CropFormSubmit = async (values, setError) => {
    setConflict(null);
    try {
      const saved = await updateCrop.mutateAsync(toUpdateCropRequest(values, crop.data.version ?? 0));
      showSuccess(`Đã lưu cây trồng ${saved.name}.`);
      // Không có trang chi tiết cây: quay lại danh sách đang lọc dở; mở thẳng đường dẫn thì về danh sách cây.
      if (location.key === "default") {
        void navigate(backTo, { replace: true });
      } else {
        void navigate(-1);
      }
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict({ cropId: id, error });
        return;
      }
      if (!setCropFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  async function reload() {
    await crop.refetch();
    setConflict(null);
    // Đổi key để form dựng lại với giá trị và version mới; dữ liệu đang nhập dở bị thay bằng bản mới.
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={`Sửa cây trồng ${crop.data.name}`} backTo={backTo} />
      <CropForm
        // Dựng lại form khi đổi sang cây khác (cùng route, khác :id) hoặc sau khi tải lại bản mới.
        key={`${id}:${formKey}`}
        mode="edit"
        defaultValues={cropToFormValues(crop.data)}
        onSubmit={handleSubmit}
        pending={updateCrop.isPending}
        backTo={backTo}
        yieldUnitLocked={crop.data.yieldUnitLocked === true}
        notice={
          conflict?.cropId === id && (
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
