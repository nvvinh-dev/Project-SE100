import { useState } from "react";
import { Navigate, useNavigate, useParams } from "react-router";
import type { UseFormSetError } from "react-hook-form";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { showSuccess } from "@/components/common/toast";
import { ExpenseForm, type ExpenseFormSubmit } from "@/features/expenses/components/ExpenseForm";
import { useCorrectExpense, useExpense } from "@/features/expenses/queries";
import {
  type ExpenseFormOutput,
  type ExpenseFormValues,
  expenseFormFields,
  expenseToFormValues,
  toCorrectExpenseRequest,
} from "@/features/expenses/schemas";
import { ApiError } from "@/lib/api-error";
import { setFieldErrors } from "@/lib/form-errors";

interface PendingCorrection {
  values: ExpenseFormOutput;
  setError: UseFormSetError<ExpenseFormValues>;
}

/**
 * `/expenses/:id/correct`: điều chỉnh khoản chi ghi nhầm (Chủ hộ, TV+TC). Form điền sẵn; bấm lưu thì hỏi lý do (BR-GEN-06),
 * gửi kèm `version` để không ghi đè thay đổi của người khác (BR-GEN-11).
 */
export function ExpenseCorrectPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const expense = useExpense(id);
  const correctExpense = useCorrectExpense(id);
  const [pendingCorrection, setPendingCorrection] = useState<PendingCorrection | null>(null);
  const [dialogError, setDialogError] = useState<unknown>(null);
  // Người khác đã sửa, hủy khoản chi sau khi mở form: báo và cho tải lại bản mới.
  const [conflict, setConflict] = useState<unknown>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/expenses/${id}`;

  if (expense.isPending) {
    return <LoadingState variant="form" />;
  }

  if (expense.isError) {
    return expense.error instanceof ApiError && expense.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={expense.error} onRetry={() => void expense.refetch()} />
    );
  }

  // BR-GEN-09: khoản chi đã hủy không điều chỉnh được; mở thẳng đường dẫn thì về trang chi tiết.
  if (expense.data.voidedAt) {
    return <Navigate to={backTo} replace />;
  }

  // Bước 1: form hợp lệ thì mở hộp thoại hỏi lý do, chưa gửi gì.
  const handleSubmit: ExpenseFormSubmit = async (values, setError) => {
    setConflict(null);
    setDialogError(null);
    correctExpense.reset();
    setPendingCorrection({ values, setError });
  };

  // Bước 2: có lý do thì gửi. Lỗi gắn được vào ô thì đóng hộp thoại để người dùng sửa ô đó.
  async function confirm(reason: string) {
    if (!pendingCorrection || !expense.data) {
      return;
    }
    try {
      await correctExpense.mutateAsync(toCorrectExpenseRequest(pendingCorrection.values, reason, expense.data.version));
      showSuccess("Đã điều chỉnh chi phí.");
      setPendingCorrection(null);
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict(error);
        setPendingCorrection(null);
        return;
      }
      if (setFieldErrors(error, pendingCorrection.setError, expenseFormFields)) {
        setPendingCorrection(null);
        return;
      }
      // SEASON_INVALID_STATE, ALREADY_VOIDED... hiện trong hộp thoại, giữ lý do đã nhập.
      setDialogError(error);
    }
  }

  async function reload() {
    await expense.refetch();
    setConflict(null);
    // Đổi key để form dựng lại với giá trị và version mới; dữ liệu đang nhập dở bị thay bằng bản mới.
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Điều chỉnh chi phí" backTo={backTo} />
      <ExpenseForm
        key={`${id}:${formKey}`}
        mode="correct"
        defaultValues={expenseToFormValues(expense.data)}
        onSubmit={handleSubmit}
        pending={correctExpense.isPending}
        backTo={backTo}
        submitLabel="Điều chỉnh chi phí"
        notice={conflict !== null && <ErrorAlert error={conflict} onRetry={() => void reload()} retryLabel="Tải lại" />}
      />
      <ReasonDialog
        open={pendingCorrection !== null}
        onOpenChange={(open) => !open && setPendingCorrection(null)}
        title="Điều chỉnh chi phí"
        description="Giá trị cũ và mới được lưu vào nhật ký cùng lý do."
        confirmLabel="Điều chỉnh chi phí"
        onConfirm={(reason) => void confirm(reason)}
        pending={correctExpense.isPending}
        error={dialogError}
      />
    </div>
  );
}
