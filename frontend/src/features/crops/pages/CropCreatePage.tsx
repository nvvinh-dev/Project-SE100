import { useLocation, useNavigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { CropForm, type CropFormSubmit } from "@/features/crops/components/CropForm";
import { useCreateCrop } from "@/features/crops/queries";
import { emptyCropFormValues, setCropFieldErrors, toCreateCropRequest } from "@/features/crops/schemas";

/** `/crops/new`: tạo cây trồng (Chủ hộ). */
export function CropCreatePage() {
  const navigate = useNavigate();
  const location = useLocation();
  const createCrop = useCreateCrop();

  const handleSubmit: CropFormSubmit = async (values, setError) => {
    try {
      const crop = await createCrop.mutateAsync(toCreateCropRequest(values));
      showSuccess(`Đã thêm cây trồng ${crop.name}.`);
      // Không có trang chi tiết cây: quay lại trang đã mở form (danh sách cây, hoặc danh sách "Bắt đầu" của hộ mới).
      // Mở thẳng đường dẫn (không có trang trước trong ứng dụng) thì về danh sách cây.
      if (location.key === "default") {
        void navigate("/crops", { replace: true });
      } else {
        void navigate(-1);
      }
    } catch (error) {
      if (!setCropFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm cây trồng" backTo="/crops" />
      <CropForm
        mode="create"
        defaultValues={emptyCropFormValues}
        onSubmit={handleSubmit}
        pending={createCrop.isPending}
        backTo="/crops"
      />
    </div>
  );
}
