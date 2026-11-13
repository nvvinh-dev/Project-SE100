import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { CustomerForm, type CustomerFormSubmit } from "@/features/sales/components/CustomerForm";
import { useCustomer, useUpdateCustomer } from "@/features/sales/queries";
import { customerToFormValues, setCustomerFieldErrors, toUpdateCustomerRequest } from "@/features/sales/schemas";
import { ApiError } from "@/lib/api-error";

/** `/customers/:id/edit`: sửa khách hàng, kể cả chuyển ngừng sử dụng (Chủ hộ, TV+TC). */
export function CustomerEditPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const customer = useCustomer(id);
  const updateCustomer = useUpdateCustomer(id);
  // Người khác đã sửa khách hàng sau khi mở form (BR-GEN-11): báo và cho tải lại bản mới.
  const [conflict, setConflict] = useState<{ customerId: string; error: unknown } | null>(null);
  const [formKey, setFormKey] = useState(0);

  if (customer.isPending) {
    return <LoadingState variant="form" />;
  }

  if (customer.isError) {
    return customer.error instanceof ApiError && customer.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={customer.error} onRetry={() => void customer.refetch()} />
    );
  }

  const handleSubmit: CustomerFormSubmit = async (values, setError) => {
    setConflict(null);
    try {
      const saved = await updateCustomer.mutateAsync(toUpdateCustomerRequest(values, customer.data.version));
      showSuccess(`Đã lưu khách hàng ${saved.name}.`);
      void navigate("/customers", { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict({ customerId: id, error });
        return;
      }
      if (!setCustomerFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  async function reload() {
    await customer.refetch();
    setConflict(null);
    setFormKey((key) => key + 1);
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={`Sửa khách hàng ${customer.data.name}`} backTo="/customers" />
      <CustomerForm
        key={`${id}:${formKey}`}
        defaultValues={customerToFormValues(customer.data)}
        onSubmit={handleSubmit}
        pending={updateCustomer.isPending}
        backTo="/customers"
        notice={
          conflict?.customerId === id && (
            <ErrorAlert error={conflict.error} onRetry={() => void reload()} retryLabel="Tải lại" />
          )
        }
      />
    </div>
  );
}
