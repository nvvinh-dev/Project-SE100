import { useNavigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { ExpenseForm, type ExpenseFormSubmit } from "@/features/expenses/components/ExpenseForm";
import { useCreateExpense } from "@/features/expenses/queries";
import { emptyExpenseFormValues, expenseFormFields, toCreateExpenseRequest } from "@/features/expenses/schemas";
import { setFieldErrors } from "@/lib/form-errors";
import { useRequestId } from "@/lib/request-id";

/**
 * `/expenses/new`: ghi chi phí (Chủ hộ, TV+TC). `?seasonId=` do ô chọn mùa vụ tự nhận khi mùa vụ đó đang canh tác.
 */
export function ExpenseCreatePage() {
  const navigate = useNavigate();
  // BR-GEN-10: sinh một lần khi mở form, bấm lưu lại sau lỗi mạng vẫn dùng đúng giá trị này.
  const requestId = useRequestId();
  const createExpense = useCreateExpense();

  const handleSubmit: ExpenseFormSubmit = async (values, setError) => {
    try {
      const saved = await createExpense.mutateAsync(toCreateExpenseRequest(values, requestId));
      showSuccess("Đã lưu chi phí.");
      void navigate(`/expenses/${saved.id}`, { replace: true });
    } catch (error) {
      // SEASON_INVALID_STATE và lỗi khác hiện nguyên văn trên form, giữ dữ liệu đã nhập.
      if (!setFieldErrors(error, setError, expenseFormFields)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Ghi chi phí" backTo="/expenses" />
      <ExpenseForm
        mode="create"
        defaultValues={emptyExpenseFormValues()}
        onSubmit={handleSubmit}
        pending={createExpense.isPending}
        backTo="/expenses"
        submitLabel="Lưu chi phí"
      />
    </div>
  );
}
