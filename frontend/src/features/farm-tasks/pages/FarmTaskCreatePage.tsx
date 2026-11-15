import { SparklesIcon } from "lucide-react";
import { useNavigate, useSearchParams } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { PermissionGate } from "@/components/common/PermissionGate";
import { showError, showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { FarmTaskForm } from "@/features/farm-tasks/components/FarmTaskForm";
import { useCreateFarmTask } from "@/features/farm-tasks/queries";
import {
  applyFarmTaskFormServerErrors,
  getEmptyFarmTaskFormValues,
  toCreateFarmTaskRequest,
} from "@/features/farm-tasks/schemas";
import { useCurrentUser } from "@/lib/auth/use-current-user";

export function FarmTaskCreatePage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const initialSeasonId = searchParams.get("seasonId") ?? "";
  const { data: user } = useCurrentUser();
  const isOwner = user?.role === "Owner";

  const createMutation = useCreateFarmTask();

  const backTo = initialSeasonId ? `/seasons/${initialSeasonId}` : "/tasks";

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <PageHeader title="Thêm công việc" backTo={backTo} />

      <PermissionGate allowed={isOwner}>
        <div className="flex flex-wrap items-center justify-between gap-2 rounded-lg border bg-muted/30 p-3 text-sm">
          <div>
            <p className="font-medium">Gợi ý lịch công việc ban đầu bằng AI</p>
            <p className="text-xs text-muted-foreground">Tính năng gợi ý bằng AI sẽ được mở ở giai đoạn tiếp theo.</p>
          </div>
          <Button type="button" variant="outline" size="sm" disabled>
            <SparklesIcon className="mr-1.5 h-4 w-4" />
            Gợi ý lịch bằng AI
          </Button>
        </div>
      </PermissionGate>

      <FarmTaskForm
        mode="create"
        defaultValues={getEmptyFarmTaskFormValues(initialSeasonId)}
        backTo={backTo}
        submitLabel="Tạo công việc"
        pending={createMutation.isPending}
        onSubmit={async (values, setError) => {
          try {
            const created = await createMutation.mutateAsync(toCreateFarmTaskRequest(values));
            if (Array.isArray(created)) {
              showSuccess(`Đã tạo ${created.length} công việc`);
              void navigate(initialSeasonId ? `/tasks?seasonId=${initialSeasonId}` : "/tasks");
              return;
            }

            showSuccess("Đã tạo công việc mới.");
            void navigate(initialSeasonId ? `/seasons/${initialSeasonId}` : `/tasks/${created.id}`);
          } catch (error) {
            if (applyFarmTaskFormServerErrors(error, setError)) {
              return;
            }
            showError(error);
            return { error };
          }
        }}
      />
    </div>
  );
}
