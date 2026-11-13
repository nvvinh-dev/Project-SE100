import { useNavigate, useSearchParams } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { SaleForm, type SaleFormSubmit } from "@/features/sales/components/SaleForm";
import { useCreateSale } from "@/features/sales/queries";
import { emptySaleFormValues, saleFormFields, toCreateSaleRequest } from "@/features/sales/schemas";
import { setFieldErrors } from "@/lib/form-errors";
import { useRequestId } from "@/lib/request-id";

/**
 * `/sales/new`: tạo giao dịch bán (Chủ hộ, TV+TC). `?seasonId=` do ô chọn mùa vụ tự nhận khi mùa vụ đang canh tác,
 * `?grade=` điền sẵn loại chất lượng (nút "Bán", "Bán ngay" của module thu hoạch).
 */
export function SaleCreatePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  // FR-SAL-06, BR-GEN-10: sinh một lần khi mở form; mất mạng rồi bấm lưu lại vẫn dùng đúng giá trị này,
  // backend nhận ra yêu cầu trùng và trả lại giao dịch đã tạo (TC-24).
  const requestId = useRequestId();
  const createSale = useCreateSale();

  const handleSubmit: SaleFormSubmit = async (values, setError) => {
    try {
      const saved = await createSale.mutateAsync(toCreateSaleRequest(values, requestId));
      showSuccess("Đã lưu giao dịch bán.");
      // Thay trang form trong lịch sử: bấm quay lại từ trang chi tiết về danh sách, không mở lại form đã gửi.
      void navigate(`/sales/${saved.id}`, { replace: true });
    } catch (error) {
      // INSUFFICIENT_PRODUCE_STOCK, CATALOG_INACTIVE, SEASON_INVALID_STATE và lỗi mạng hiện nguyên văn trên form,
      // giữ dữ liệu đã nhập để bấm lưu lại.
      if (!setFieldErrors(error, setError, saleFormFields)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Bán hàng" backTo="/sales" />
      <SaleForm
        mode="create"
        defaultValues={emptySaleFormValues(searchParams.get("grade"))}
        onSubmit={handleSubmit}
        pending={createSale.isPending}
        backTo="/sales"
        submitLabel="Lưu giao dịch"
      />
    </div>
  );
}
