import { useState } from "react";
import type { UseFormSetError } from "react-hook-form";
import { Navigate, useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { showSuccess } from "@/components/common/toast";
import { SaleForm, type SaleFormSubmit } from "@/features/sales/components/SaleForm";
import { useCorrectSale, useSale } from "@/features/sales/queries";
import {
  saleFormFields,
  type SaleFormOutput,
  type SaleFormValues,
  saleToFormValues,
  toCorrectSaleRequest,
} from "@/features/sales/schemas";
import { ApiError } from "@/lib/api-error";
import { setFieldErrors } from "@/lib/form-errors";

interface PendingCorrection {
  values: SaleFormOutput;
  setError: UseFormSetError<SaleFormValues>;
}

/**
 * `/sales/:id/correct`: điều chỉnh giao dịch bán ghi nhầm (Chủ hộ, TV+TC). Form điền sẵn gồm cả liên kết phiếu thu;
 * bấm lưu thì hỏi lý do (BR-GEN-06), gửi kèm `version` (BR-GEN-11).
 */
export function SaleCorrectPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const sale = useSale(id);
  const correctSale = useCorrectSale(id);
  const [pendingCorrection, setPendingCorrection] = useState<PendingCorrection | null>(null);
  const [dialogError, setDialogError] = useState<unknown>(null);
  const [conflict, setConflict] = useState<unknown>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/sales/${id}`;

  if (sale.isPending) {
    return <LoadingState variant="form" />;
  }

  if (sale.isError) {
    return sale.error instanceof ApiError && sale.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={sale.error} onRetry={() => void sale.refetch()} />
    );
  }

  // Giao dịch đã hủy, mùa vụ không còn đang canh tác: không điều chỉnh được, mở thẳng đường dẫn thì về trang chi tiết.
  if (sale.data.voidedAt || sale.data.seasonStatus !== "InProgress") {
    return <Navigate to={backTo} replace />;
  }

  // Bước 1: form hợp lệ thì mở hộp thoại hỏi lý do, chưa gửi gì.
  const handleSubmit: SaleFormSubmit = async (values, setError) => {
    setConflict(null);
    setDialogError(null);
    correctSale.reset();
    setPendingCorrection({ values, setError });
  };

  // Bước 2: có lý do thì gửi. Lỗi gắn được vào ô thì đóng hộp thoại để người dùng sửa ô đó.
  async function confirm(reason: string) {
    if (!pendingCorrection || !sale.data) {
      return;
    }
    try {
      await correctSale.mutateAsync(toCorrectSaleRequest(pendingCorrection.values, reason, sale.data.version));
      showSuccess("Đã điều chỉnh giao dịch bán.");
      setPendingCorrection(null);
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict(error);
        setPendingCorrection(null);
        return;
      }
      if (setFieldErrors(error, pendingCorrection.setError, saleFormFields)) {
        setPendingCorrection(null);
        return;
      }
      // INSUFFICIENT_PRODUCE_STOCK, REVENUE_BELOW_PAID, CATALOG_INACTIVE... hiện nguyên văn trong hộp thoại.
      setDialogError(error);
    }
  }

  async function reload() {
    await sale.refetch();
    setConflict(null);
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Điều chỉnh giao dịch bán" backTo={backTo} />
      <SaleForm
        key={`${id}:${formKey}`}
        mode="correct"
        defaultValues={saleToFormValues(sale.data)}
        defaultYieldUnit={sale.data.yieldUnit}
        // BR-SAL-03: tồn có thể bán gồm cả lượng của chính giao dịch này khi giữ nguyên loại.
        stockCredit={{ seasonId: sale.data.seasonId, qualityGrade: sale.data.qualityGrade, quantity: sale.data.quantity }}
        onSubmit={handleSubmit}
        pending={correctSale.isPending}
        backTo={backTo}
        submitLabel="Điều chỉnh giao dịch"
        notice={conflict !== null && <ErrorAlert error={conflict} onRetry={() => void reload()} retryLabel="Tải lại" />}
      />
      <ReasonDialog
        open={pendingCorrection !== null}
        onOpenChange={(open) => !open && setPendingCorrection(null)}
        title="Điều chỉnh giao dịch bán"
        description="Giá trị cũ và mới được lưu vào nhật ký cùng lý do."
        confirmLabel="Điều chỉnh giao dịch"
        onConfirm={(reason) => void confirm(reason)}
        pending={correctSale.isPending}
        error={dialogError}
      />
    </div>
  );
}
