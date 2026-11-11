import { useNavigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { SeasonForm, type SeasonFormSubmit } from "@/features/seasons/components/SeasonForm";
import { useCreateSeason } from "@/features/seasons/queries";
import {
  getEmptySeasonFormValues,
  setSeasonFieldErrors,
  toCreateSeasonRequest,
} from "@/features/seasons/schemas";

/** `/seasons/new`: tạo mùa vụ mới (Chủ hộ, FR-SEA-01). */
export function SeasonCreatePage() {
  const navigate = useNavigate();
  const createSeason = useCreateSeason();

  const handleSubmit: SeasonFormSubmit = async (values, setError) => {
    try {
      const season = await createSeason.mutateAsync(toCreateSeasonRequest(values));
      showSuccess(`Đã tạo mùa vụ ${season.cropName} · ${season.plotCode}.`);
      void navigate(`/seasons/${season.id}`, { replace: true });
    } catch (error) {
      if (!setSeasonFieldErrors(error, setError)) {
        return { error };
      }
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm mùa vụ" backTo="/seasons" />
      <SeasonForm
        mode="create"
        defaultValues={getEmptySeasonFormValues()}
        onSubmit={handleSubmit}
        pending={createSeason.isPending}
        backTo="/seasons"
      />
    </div>
  );
}
