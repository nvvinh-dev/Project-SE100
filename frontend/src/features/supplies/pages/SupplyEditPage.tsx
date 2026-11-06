import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { SupplyForm, type SupplyFormSubmit } from "@/features/supplies/components/SupplyForm";
import { useSupply, useUpdateSupply } from "@/features/supplies/queries";
import { setSupplyFieldErrors, supplyToFormValues, toUpdateSupplyRequest } from "@/features/supplies/schemas";
import { ApiError } from "@/lib/api-error";

/** `/supplies/:id/edit`: sửa vật tư, kể cả đổi trạng thái (Chủ hộ). */
export function SupplyEditPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const supply = useSupply(id);
  const updateSupply = useUpdateSupply(id);
  // Người khác đã sửa vật tư sau khi mở form (BR-GEN-11): báo và cho tải lại bản mới.
  // Ghi kèm mã vật tư để không hiện nhầm sang vật tư khác khi đổi :id mà trang không dựng lại.
  const [conflict, setConflict] = useState<{ supplyId: string; error: unknown } | null>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/supplies/${id}`;

  if (supply.isPending) {
    return <LoadingState variant="form" />;
  }

  if (supply.isError) {
    return isNotFound(supply.error) ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={supply.error} onRetry={() => void supply.refetch()} />
    );
  }

  const handleSubmit: SupplyFormSubmit = async (values, setError) => {
    setConflict(null);
    try {
      const saved = await updateSupply.mutateAsync(toUpdateSupplyRequest(values, supply.data.version));
      showSuccess(`Đã lưu vật tư ${saved.code}.`);
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict({ supplyId: id, error });
        return;
      }
      if (!setSupplyFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  async function reload() {
    await supply.refetch();
    setConflict(null);
    // Đổi key để form dựng lại với giá trị và version mới; dữ liệu đang nhập dở bị thay bằng bản mới.
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={`Sửa vật tư ${supply.data.code}`} backTo={backTo} />
      <SupplyForm
        // Dựng lại form khi đổi sang vật tư khác (cùng route, khác :id) hoặc sau khi tải lại bản mới.
        key={`${id}:${formKey}`}
        mode="edit"
        defaultValues={supplyToFormValues(supply.data)}
        unitLocked={supply.data.unitLocked}
        onSubmit={handleSubmit}
        pending={updateSupply.isPending}
        backTo={backTo}
        notice={
          conflict?.supplyId === id && (
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
