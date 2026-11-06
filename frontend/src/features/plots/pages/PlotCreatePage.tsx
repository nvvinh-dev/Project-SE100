import { useNavigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { PlotForm, type PlotFormSubmit } from "@/features/plots/components/PlotForm";
import { useCreatePlot } from "@/features/plots/queries";
import { emptyPlotFormValues, setPlotFieldErrors, toCreatePlotRequest } from "@/features/plots/schemas";

/** `/plots/new`: tạo thửa đất (Chủ hộ). */
export function PlotCreatePage() {
  const navigate = useNavigate();
  const createPlot = useCreatePlot();

  const handleSubmit: PlotFormSubmit = async (values, setError) => {
    try {
      const plot = await createPlot.mutateAsync(toCreatePlotRequest(values));
      showSuccess(`Đã thêm thửa đất ${plot.code}.`);
      // Thay trang form trong lịch sử: bấm quay lại từ trang chi tiết về danh sách, không mở lại form trống.
      void navigate(`/plots/${plot.id}`, { replace: true });
    } catch (error) {
      if (!setPlotFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm thửa đất" backTo="/plots" />
      <PlotForm
        mode="create"
        defaultValues={emptyPlotFormValues}
        onSubmit={handleSubmit}
        pending={createPlot.isPending}
        backTo="/plots"
      />
    </div>
  );
}
