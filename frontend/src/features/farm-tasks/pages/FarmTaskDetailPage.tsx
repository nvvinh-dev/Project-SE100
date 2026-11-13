import { CheckCircle2Icon, EllipsisVerticalIcon, PencilIcon, RepeatIcon, Trash2Icon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { PermissionGate } from "@/components/common/PermissionGate";
import { QuantityText } from "@/components/common/QuantityText";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showError, showSuccess } from "@/components/common/toast";
import { Button, buttonVariants } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { useCancelFarmTask, useCancelRecurrenceGroup, useFarmTask } from "@/features/farm-tasks/queries";
import { formatFarmTaskTitle } from "@/features/farm-tasks/task-format";
import type { FarmTaskDetail, FarmTaskResponse } from "@/features/farm-tasks/types";
import { ApiError } from "@/lib/api-error";
import { useCurrentUser } from "@/lib/auth/use-current-user";

function isOwnerOrMemberDetail(task: FarmTaskDetail): task is FarmTaskResponse {
  return "createdById" in task;
}

export function FarmTaskDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: user } = useCurrentUser();
  const taskQuery = useFarmTask(id);
  const cancelMutation = useCancelFarmTask(id);
  const cancelGroupMutation = useCancelRecurrenceGroup();

  const [cancelOpen, setCancelOpen] = useState(false);
  const [cancelGroupOpen, setCancelGroupOpen] = useState(false);

  if (taskQuery.isPending) {
    return <LoadingState variant="detail" />;
  }

  if (taskQuery.isError) {
    return taskQuery.error instanceof ApiError && taskQuery.error.code === "NOT_FOUND" ? (
      <NotFoundPage />
    ) : (
      <ErrorAlert error={taskQuery.error} onRetry={() => void taskQuery.refetch()} />
    );
  }

  const task = taskQuery.data;
  const isWorker = user?.role === "Worker";
  const isHouseholdRole = user !== undefined && !isWorker;
  const isPendingOrWaiting = task.status === "Pending" || task.status === "WaitingForSupply";
  const fullDetail = isOwnerOrMemberDetail(task) ? task : null;
  const recurrenceGroupId = fullDetail?.recurrenceGroupId ?? null;
  const canEditOrCancel =
    isHouseholdRole && isPendingOrWaiting && (user?.role === "Owner" || fullDetail?.createdById === user?.id);
  const canRecordResult =
    user !== undefined &&
    (isPendingOrWaiting || task.status === "Done") &&
    (user.role === "Owner" ||
      user.role === "Worker" ||
      (user.role === "Member" && (task.assigneeId === user.id || fullDetail?.createdById === user.id)));
  const title = formatFarmTaskTitle(task);

  async function handleCancelTask() {
    try {
      await cancelMutation.mutateAsync({ version: task.version });
      showSuccess("Đã hủy công việc.");
      setCancelOpen(false);
    } catch (error) {
      showError(error);
    }
  }

  async function handleCancelGroup() {
    if (!recurrenceGroupId) {
      return;
    }
    try {
      const result = await cancelGroupMutation.mutateAsync(recurrenceGroupId);
      showSuccess(`Đã hủy ${result.cancelledCount} công việc chưa thực hiện trong nhóm lặp.`);
      setCancelGroupOpen(false);
      void navigate("/tasks");
    } catch (error) {
      showError(error);
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={title}
        description={`${task.plotCode} · ${task.cropName}`}
        backTo="/tasks"
        action={
          <div className="flex items-center gap-2">
            <PermissionGate allowed={canRecordResult}>
              <Link to={`/tasks/${task.id}/result`} className={buttonVariants()}>
                <CheckCircle2Icon data-icon="inline-start" />
                {task.status === "Done" ? "Bổ sung kết quả" : "Ghi kết quả"}
              </Link>
            </PermissionGate>
            <PermissionGate allowed={canEditOrCancel}>
              <DropdownMenu>
                <DropdownMenuTrigger asChild>
                  <Button variant="ghost" size="icon" aria-label="Thao tác khác">
                    <EllipsisVerticalIcon />
                  </Button>
                </DropdownMenuTrigger>
                <DropdownMenuContent align="end">
                  <DropdownMenuItem asChild className="min-h-11">
                    <Link to={`/tasks/${task.id}/edit`}>
                      <PencilIcon />
                      Sửa công việc
                    </Link>
                  </DropdownMenuItem>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    className="min-h-11 text-destructive focus:text-destructive"
                    onSelect={() => setCancelOpen(true)}
                  >
                    <Trash2Icon />
                    Hủy công việc
                  </DropdownMenuItem>
                  {recurrenceGroupId && (
                    <DropdownMenuItem
                      className="min-h-11 text-destructive focus:text-destructive"
                      onSelect={() => setCancelGroupOpen(true)}
                    >
                      <RepeatIcon />
                      Hủy cả nhóm lặp
                    </DropdownMenuItem>
                  )}
                </DropdownMenuContent>
              </DropdownMenu>
            </PermissionGate>
          </div>
        }
      />

      <section className="flex flex-col gap-4 rounded-lg border p-4">
        <div className="flex flex-wrap items-center gap-2">
          <StatusBadge kind="FarmTaskStatus" value={task.status} />
          {task.isOverdue && <StatusBadge kind="Overdue" />}
          {recurrenceGroupId && (
            <span className="inline-flex items-center gap-1 rounded-md border px-2 py-0.5 text-xs text-muted-foreground">
              <RepeatIcon className="h-3 w-3" /> Thuộc nhóm lặp
            </span>
          )}
        </div>

        <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
          <InfoRow label="Mùa vụ">
            {isWorker ? (
              `${task.plotCode} · ${task.cropName}`
            ) : (
              <Link to={`/seasons/${task.seasonId}`} className="text-primary underline-offset-4 hover:underline">
                {task.plotCode} · {task.cropName}
              </Link>
            )}
          </InfoRow>
          <InfoRow label="Ngày dự kiến">
            <DateText value={task.scheduledDate} />
          </InfoRow>
          {!isWorker && <InfoRow label="Người thực hiện">{task.assigneeName ?? "Chưa giao người"}</InfoRow>}
          <InfoRow label="Thời lượng dự kiến">
            {task.plannedDurationMinutes !== null ? `${task.plannedDurationMinutes} phút` : "—"}
          </InfoRow>
          <InfoRow label="Ghi chú" wide>
            {task.note ?? "—"}
          </InfoRow>
        </dl>
      </section>

      <section className="flex flex-col gap-3 rounded-lg border p-4">
        <h2 className="text-sm font-semibold">Vật tư của công việc</h2>
        {task.supplyUsages.length === 0 ? (
          <p className="text-sm text-muted-foreground">Không có vật tư khai báo cho công việc này.</p>
        ) : (
          <ul className="divide-y">
            {task.supplyUsages.map((item) => (
              <li key={item.id} className="flex flex-wrap items-center justify-between gap-2 py-2.5 text-sm">
                <span className="font-medium">{item.supplyName}</span>
                <div className="flex flex-wrap items-center gap-4 text-muted-foreground">
                  <span>
                    Dự kiến:{" "}
                    {item.plannedQuantity !== null ? (
                      <QuantityText value={item.plannedQuantity} unit={item.unit} />
                    ) : (
                      "—"
                    )}
                  </span>
                  {fullDetail && "actualQuantity" in item && item.actualQuantity !== null && (
                    <span className="font-medium text-foreground">
                      Thực dùng: <QuantityText value={item.actualQuantity} unit={item.unit} />
                    </span>
                  )}
                </div>
              </li>
            ))}
          </ul>
        )}
      </section>

      {(task.status === "Done" ||
        task.performedDate !== null ||
        task.actualDurationMinutes !== null ||
        task.abnormalNote !== null ||
        task.fieldNote !== null) && (
        <section className="flex flex-col gap-3 rounded-lg border p-4">
          <h2 className="text-sm font-semibold">Kết quả thực hiện</h2>
          <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
            <InfoRow label="Ngày thực hiện">
              {task.performedDate ? <DateText value={task.performedDate} /> : "—"}
            </InfoRow>
            <InfoRow label="Thời lượng thực tế">
              {task.actualDurationMinutes !== null ? `${task.actualDurationMinutes} phút` : "—"}
            </InfoRow>
            <InfoRow label="Ghi chú bất thường" wide>
              {task.abnormalNote ?? "—"}
            </InfoRow>
            <InfoRow label="Ghi chú hiện trường" wide>
              {task.fieldNote ?? "—"}
            </InfoRow>
          </dl>
        </section>
      )}

      {fullDetail && (
        <section className="flex flex-col gap-3 rounded-lg border p-4 text-sm text-muted-foreground">
          <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
            <InfoRow label="Người tạo">{fullDetail.createdByName ?? "—"}</InfoRow>
            <InfoRow label="Ngày tạo">
              <DateText value={fullDetail.createdAt} withTime />
            </InfoRow>
            {fullDetail.updatedAt && (
              <InfoRow label="Cập nhật gần nhất">
                <DateText value={fullDetail.updatedAt} withTime />
              </InfoRow>
            )}
            {fullDetail.cancelledAt && (
              <InfoRow label="Thời điểm hủy">
                <DateText value={fullDetail.cancelledAt} withTime />
              </InfoRow>
            )}
          </dl>
        </section>
      )}

      <ConfirmDialog
        open={cancelOpen}
        onOpenChange={setCancelOpen}
        title="Hủy công việc này?"
        description="Công việc sẽ chuyển sang trạng thái Đã hủy và không thể mở lại."
        confirmLabel="Xác nhận hủy"
        destructive
        pending={cancelMutation.isPending}
        onConfirm={() => void handleCancelTask()}
      />

      <ConfirmDialog
        open={cancelGroupOpen}
        onOpenChange={setCancelGroupOpen}
        title="Hủy toàn bộ công việc chưa làm trong nhóm lặp?"
        description="Mọi công việc đang Chưa làm hoặc Chờ bổ sung vật tư thuộc nhóm lặp này sẽ bị hủy. Công việc đã Hoàn thành vẫn được giữ nguyên."
        confirmLabel="Hủy nhóm lặp"
        destructive
        pending={cancelGroupMutation.isPending}
        onConfirm={() => void handleCancelGroup()}
      />
    </div>
  );
}

function InfoRow({ label, wide = false, children }: { label: string; wide?: boolean; children: ReactNode }) {
  return (
    <div className={wide ? "sm:col-span-2" : undefined}>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words whitespace-pre-line">{children}</dd>
    </div>
  );
}
