import { useNavigate } from "react-router";
import { toast } from "sonner";
import { PageHeader } from "@/components/common/PageHeader";
import { HarvestForm, type HarvestFormSubmit } from "@/features/harvests/components/HarvestForm";
import { useCreateHarvest } from "@/features/harvests/queries";
import { emptyHarvestFormValues, harvestFormFields, toCreateHarvestRequest } from "@/features/harvests/schemas";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { setFieldErrors } from "@/lib/form-errors";
import { useRequestId } from "@/lib/request-id";

/**
 * `/harvests/new`: ghi phiếu thu hoạch cho mùa vụ Đang canh tác (FR-HAR-01).
 * Hỗ trợ `?seasonId=` khi mở từ trang chi tiết mùa vụ.
 */
export function HarvestCreatePage() {
  const navigate = useNavigate();
  const { data: user } = useCurrentUser();
  // BR-GEN-10: sinh một lần khi mở form để chống tạo trùng khi gửi lại.
  const requestId = useRequestId();
  const createHarvest = useCreateHarvest();

  const canSell = Boolean(user && canAccess(user, ACCESS.financeManager));

  const handleSubmit: HarvestFormSubmit = async (values, setError) => {
    try {
      const saved = await createHarvest.mutateAsync(toCreateHarvestRequest(values, requestId));
      if (canSell) {
        toast.success("Đã lưu phiếu thu hoạch.", {
          duration: 5_000,
          action: {
            label: "Bán ngay",
            onClick: () => void navigate(`/sales/new?seasonId=${saved.seasonId}&grade=${saved.qualityGrade}`),
          },
        });
      } else {
        toast.success("Đã lưu phiếu thu hoạch.", { duration: 3_000 });
      }
      void navigate(`/harvests/${saved.id}`, { replace: true });
    } catch (error) {
      if (!setFieldErrors(error, setError, harvestFormFields)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Ghi thu hoạch" backTo="/harvests" />
      <HarvestForm
        mode="create"
        defaultValues={emptyHarvestFormValues()}
        onSubmit={handleSubmit}
        pending={createHarvest.isPending}
        backTo="/harvests"
        submitLabel="Lưu phiếu thu hoạch"
      />
    </div>
  );
}
