import { useNavigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { CustomerForm, type CustomerFormSubmit } from "@/features/sales/components/CustomerForm";
import { useCreateCustomer } from "@/features/sales/queries";
import { emptyCustomerFormValues, setCustomerFieldErrors, toCreateCustomerRequest } from "@/features/sales/schemas";

/** `/customers/new`: thêm khách hàng (Chủ hộ, TV+TC). */
export function CustomerCreatePage() {
  const navigate = useNavigate();
  const createCustomer = useCreateCustomer();

  const handleSubmit: CustomerFormSubmit = async (values, setError) => {
    try {
      const customer = await createCustomer.mutateAsync(toCreateCustomerRequest(values));
      showSuccess(`Đã thêm khách hàng ${customer.name}.`);
      void navigate("/customers", { replace: true });
    } catch (error) {
      if (!setCustomerFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm khách hàng" backTo="/customers" />
      <CustomerForm
        defaultValues={emptyCustomerFormValues}
        onSubmit={handleSubmit}
        pending={createCustomer.isPending}
        backTo="/customers"
      />
    </div>
  );
}
