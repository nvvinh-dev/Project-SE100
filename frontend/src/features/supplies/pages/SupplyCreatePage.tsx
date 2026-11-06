import { useNavigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { SupplyForm, type SupplyFormSubmit } from "@/features/supplies/components/SupplyForm";
import { useCreateSupply } from "@/features/supplies/queries";
import { emptySupplyFormValues, setSupplyFieldErrors, toCreateSupplyRequest } from "@/features/supplies/schemas";

/** `/supplies/new`: tạo vật tư (Chủ hộ). */
export function SupplyCreatePage() {
  const navigate = useNavigate();
  const createSupply = useCreateSupply();

  const handleSubmit: SupplyFormSubmit = async (values, setError) => {
    try {
      const supply = await createSupply.mutateAsync(toCreateSupplyRequest(values));
      showSuccess(`Đã thêm vật tư ${supply.code}.`);
      // Thay trang form trong lịch sử: bấm quay lại từ trang chi tiết về danh sách, không mở lại form trống.
      void navigate(`/supplies/${supply.id}`, { replace: true });
    } catch (error) {
      if (!setSupplyFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm vật tư" backTo="/supplies" />
      <SupplyForm
        mode="create"
        defaultValues={emptySupplyFormValues}
        onSubmit={handleSubmit}
        pending={createSupply.isPending}
        backTo="/supplies"
      />
    </div>
  );
}
