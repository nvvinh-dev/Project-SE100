import { useState } from "react";
import { useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showError, showSuccess } from "@/components/common/toast";
import { FarmTaskForm, type FarmTaskFormSubmit } from "@/features/farm-tasks/components/FarmTaskForm";
import { useFarmTask, useUpdateFarmTask } from "@/features/farm-tasks/queries";
import {
  applyFarmTaskFormServerErrors,
  farmTaskToFormValues,
  toUpdateFarmTaskRequest,
} from "@/features/farm-tasks/schemas";
import type { FarmTaskDetail, FarmTaskResponse } from "@/features/farm-tasks/types";
import { ApiError } from "@/lib/api-error";

function isEditableDetail(task: FarmTaskDetail): task is FarmTaskResponse {
  return "createdById" in task;
}

export function FarmTaskEditPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const taskQuery = useFarmTask(id);
  const updateMutation = useUpdateFarmTask(id);
  const [conflict, setConflict] = useState<{ taskId: string; error: unknown } | null>(null);
  const [formKey, setFormKey] = useState(0);
  const backTo = `/tasks/${id}`;

  if (taskQuery.isPending) {
    return <LoadingState variant="form" />;
  }

  if (taskQuery.isError) {
    return taskQuery.error instanceof ApiError && taskQuery.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={taskQuery.error} onRetry={() => void taskQuery.refetch()} />
    );
  }

  const task = taskQuery.data;
  if (!isEditableDetail(task)) {
    return <NotFoundPage />;
  }

  const handleSubmit: FarmTaskFormSubmit = async (values, setError) => {
    setConflict(null);
    try {
      await updateMutation.mutateAsync(toUpdateFarmTaskRequest(values, task.version));
      showSuccess("Đã lưu thay đổi công việc.");
      void navigate(backTo, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "CONCURRENCY_CONFLICT") {
        setConflict({ taskId: id, error });
        return;
      }
      if (applyFarmTaskFormServerErrors(error, setError)) {
        return;
      }
      showError(error);
      return { error };
    }
  };

  async function reload() {
    await taskQuery.refetch();
    setConflict(null);
    setFormKey((k) => k + 1);
  }

  return (
    <div className="mx-auto flex w-full max-w-2xl flex-col gap-6">
      <PageHeader title="Sửa công việc" backTo={backTo} />
      <FarmTaskForm
        key={`${id}:${formKey}`}
        mode="edit"
        defaultValues={farmTaskToFormValues(task)}
        seasonFallbackLabel={`${task.plotCode} · ${task.cropName}`}
        onSubmit={handleSubmit}
        pending={updateMutation.isPending}
        backTo={backTo}
        submitLabel="Lưu công việc"
        notice={
          conflict?.taskId === id && (
            <ErrorAlert error={conflict.error} onRetry={() => void reload()} retryLabel="Tải lại" />
          )
        }
      />
    </div>
  );
}
