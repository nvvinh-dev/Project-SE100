import { zodResolver } from "@hookform/resolvers/zod";
import { PlusIcon, SparklesIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, type Control, useFieldArray, useForm, useWatch } from "react-hook-form";
import { Link, useNavigate, useParams } from "react-router";
import { NotFoundPage } from "@/app/pages/NotFoundPage";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { showSuccess, showWarnings } from "@/components/common/toast";
import { Button, buttonVariants } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { useFarmTask, useRecordFarmTaskResult } from "@/features/farm-tasks/queries";
import {
  applyRecordResultServerErrors,
  farmTaskToResultFormValues,
  hasSupplyAdjustmentRequiringReason,
  type RecordResultFormOutput,
  recordResultFormSchema,
  type RecordResultFormValues,
  toRecordFarmTaskResultRequest,
} from "@/features/farm-tasks/result-schemas";
import { formatFarmTaskTitle } from "@/features/farm-tasks/task-format";
import type { FarmTaskDetail, FarmTaskResponse } from "@/features/farm-tasks/types";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import { useSupply } from "@/features/supplies/queries";
import { ApiError } from "@/lib/api-error";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { unitLabels } from "@/lib/enum-labels";

function isHouseholdTask(task: FarmTaskDetail): task is FarmTaskResponse {
  return "createdById" in task;
}

export function FarmTaskResultPage() {
  const { id = "" } = useParams();
  const { data: user } = useCurrentUser();
  const taskQuery = useFarmTask(id);

  if (taskQuery.isPending || !user) {
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
  const fullDetail = isHouseholdTask(task) ? task : null;
  const isWorker = user.role === "Worker";
  const canRecord =
    user.role === "Owner" ||
    user.role === "Worker" ||
    (user.role === "Member" && (task.assigneeId === user.id || fullDetail?.createdById === user.id));

  if (!canRecord) {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Ghi kết quả công việc" backTo={`/tasks/${task.id}`} />
        <p className="rounded-lg border p-4 text-sm text-destructive">
          Bạn chỉ được ghi kết quả cho công việc do mình tạo hoặc được giao cho mình.
        </p>
        <div>
          <Link to={`/tasks/${task.id}`} className={buttonVariants({ variant: "outline" })}>
            Quay lại chi tiết
          </Link>
        </div>
      </div>
    );
  }

  if (task.status === "Cancelled") {
    return (
      <div className="flex flex-col gap-4">
        <PageHeader title="Ghi kết quả công việc" backTo={`/tasks/${task.id}`} />
        <p className="rounded-lg border p-4 text-sm text-muted-foreground">
          Công việc đã bị hủy nên không thể ghi hoặc bổ sung kết quả.
        </p>
        <div>
          <Link to={`/tasks/${task.id}`} className={buttonVariants({ variant: "outline" })}>
            Quay lại chi tiết
          </Link>
        </div>
      </div>
    );
  }

  return <FarmTaskResultForm key={`${task.id}-${task.version}`} task={task} isWorker={isWorker} />;
}

interface FarmTaskResultFormProps {
  task: FarmTaskDetail;
  isWorker: boolean;
}

function FarmTaskResultForm({ task, isWorker }: FarmTaskResultFormProps) {
  const navigate = useNavigate();
  const recordMutation = useRecordFarmTaskResult(task.id);
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [pendingValues, setPendingValues] = useState<RecordResultFormOutput | null>(null);
  const [reasonOpen, setReasonOpen] = useState(false);
  const [reasonError, setReasonError] = useState<unknown>(null);
  const [insufficientError, setInsufficientError] = useState<ApiError | null>(null);

  const isAlreadyDone = task.status === "Done";
  const form = useForm<RecordResultFormValues, unknown, RecordResultFormOutput>({
    resolver: zodResolver(recordResultFormSchema(isWorker)),
    defaultValues: farmTaskToResultFormValues(task, isWorker),
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: "supplies",
  });

  const currentStatus = useWatch({ control: form.control, name: "status" });
  const isWaitingForSupply = !isWorker && currentStatus === "WaitingForSupply";
  const initialSupplyNames = new Map(
    isHouseholdTask(task) ? task.supplyUsages.map((u) => [u.supplyId, u.supplyName]) : [],
  );

  async function executeSubmit(values: RecordResultFormOutput, reason: string | null) {
    setSubmitError(null);
    setReasonError(null);

    try {
      const request = toRecordFarmTaskResultRequest(values, isWorker, task.version, reason);
      const outcome = await recordMutation.mutateAsync(request);
      setReasonOpen(false);
      setPendingValues(null);
      showSuccess(
        outcome.data.status === "WaitingForSupply"
          ? "Đã chuyển công việc sang Chờ bổ sung vật tư."
          : isAlreadyDone
            ? "Đã bổ sung kết quả công việc."
            : "Đã ghi kết quả công việc.",
      );
      showWarnings(outcome.warnings);
      void navigate(`/tasks/${task.id}`, { replace: true });
    } catch (error) {
      if (error instanceof ApiError && error.code === "INSUFFICIENT_STOCK") {
        setReasonOpen(false);
        setInsufficientError(error);
        return;
      }

      if (reasonOpen) {
        setReasonError(error);
        return;
      }

      if (!applyRecordResultServerErrors(error, form.setError)) {
        setSubmitError(error);
      }
    }
  }

  function handleFormSubmit(values: RecordResultFormOutput) {
    if (!isWorker && values.status === "Done" && hasSupplyAdjustmentRequiringReason(task, values.supplies)) {
      setPendingValues(values);
      setReasonError(null);
      setReasonOpen(true);
      return;
    }

    void executeSubmit(values, null);
  }

  function handleSwitchToWaitingForSupply() {
    setInsufficientError(null);
    form.setValue("status", "WaitingForSupply", { shouldDirty: true, shouldValidate: true });
    const current = form.getValues();
    void executeSubmit(
      {
        status: "WaitingForSupply",
        performedDate: "",
        actualDurationMinutes: current.actualDurationMinutes,
        abnormalNote: current.abnormalNote,
        fieldNote: current.fieldNote,
        supplies: current.supplies,
      },
      null,
    );
  }

  function handleAdjustQuantity() {
    const failedSupplyId =
      typeof insufficientError?.details?.supplyId === "string" ? insufficientError.details.supplyId : null;
    setInsufficientError(null);
    if (failedSupplyId) {
      const index = form.getValues("supplies").findIndex((row) => row.supplyId === failedSupplyId);
      if (index >= 0) {
        form.setFocus(`supplies.${index}.actualQuantity`);
      }
    }
  }

  function handlePickAnotherSupply() {
    setInsufficientError(null);
    const hasBlankRow = form.getValues("supplies").some((row) => row.supplyId === "");
    if (!hasBlankRow) {
      append({
        supplyId: "",
        actualQuantity: "",
        plannedQuantity: null,
        initialActualQuantity: null,
        initialStockTransactionId: null,
      });
    }
  }

  const pageTitle = isAlreadyDone ? "Bổ sung kết quả" : "Ghi kết quả công việc";

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title={pageTitle}
        description={`${formatFarmTaskTitle(task)} · ${task.plotCode} · ${task.cropName}`}
        backTo={`/tasks/${task.id}`}
      />

      {!isWorker && (
        <div className="flex flex-col gap-2 rounded-lg border border-dashed p-3 sm:flex-row sm:items-center sm:justify-between">
          <div className="text-sm text-muted-foreground">
            Bạn có thể ghi kết quả thủ công bên dưới. Nhập nhanh bằng mô tả tiếng Việt sẽ mở khi kết nối trợ lý AI.
          </div>
          <Button type="button" variant="outline" className="min-h-11 shrink-0" disabled>
            <SparklesIcon data-icon="inline-start" />
            Nhập nhanh bằng câu tự nhiên
          </Button>
        </div>
      )}

      <form noValidate onSubmit={form.handleSubmit(handleFormSubmit)} className="flex flex-col gap-6">
        <FieldGroup>
          {!isWorker && !isAlreadyDone && (
            <Controller
              name="status"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="result-status">Trạng thái kết quả</FieldLabel>
                  <NativeSelect
                    id="result-status"
                    value={field.value}
                    onChange={(event) => field.onChange(event.target.value)}
                    aria-invalid={fieldState.invalid}
                  >
                    <NativeSelectOption value="Done">Hoàn thành công việc</NativeSelectOption>
                    <NativeSelectOption value="WaitingForSupply">Chờ bổ sung vật tư</NativeSelectOption>
                  </NativeSelect>
                  <FieldDescription>
                    Chọn &ldquo;Chờ bổ sung vật tư&rdquo; nếu kho chưa đủ vật tư để hoàn thành hôm nay.
                  </FieldDescription>
                  <FieldError errors={[fieldState.error]} />
                </Field>
              )}
            />
          )}

          {!isWaitingForSupply && (
            <Controller
              name="performedDate"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="result-performed-date">
                    Ngày thực hiện <span className="text-destructive">*</span>
                  </FieldLabel>
                  <Input
                    {...field}
                    id="result-performed-date"
                    type="date"
                    required
                    aria-invalid={fieldState.invalid}
                  />
                  <FieldError errors={[fieldState.error]} />
                </Field>
              )}
            />
          )}

          <Controller
            name="actualDurationMinutes"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="result-duration">Thời lượng thực tế (phút)</FieldLabel>
                <Input
                  {...field}
                  id="result-duration"
                  inputMode="numeric"
                  placeholder={
                    task.plannedDurationMinutes !== null ? `Dự kiến ${task.plannedDurationMinutes} phút` : "Ví dụ: 60"
                  }
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="abnormalNote"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="result-abnormal-note">Dấu hiệu bất thường / sâu bệnh / thời tiết</FieldLabel>
                <Textarea
                  {...field}
                  id="result-abnormal-note"
                  rows={2}
                  placeholder="Ghi nhận bất thường nếu có..."
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="fieldNote"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="result-field-note">Ghi chú hiện trường</FieldLabel>
                <Textarea
                  {...field}
                  id="result-field-note"
                  rows={3}
                  placeholder="Tình trạng ruộng, độ ẩm đất, nhận xét sau khi làm..."
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {!isWorker && !isWaitingForSupply && (
          <section className="flex flex-col gap-3 rounded-lg border p-4">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <div>
                <h2 className="text-sm font-semibold">Vật tư thực dùng</h2>
                <p className="text-xs text-muted-foreground">
                  Lưu hoàn thành sẽ tự động xuất kho theo lượng thực dùng. Để trống nếu không xuất vật tư đó.
                </p>
              </div>
              <Button
                type="button"
                variant="outline"
                size="sm"
                className="min-h-11"
                onClick={() =>
                  append({
                    supplyId: "",
                    actualQuantity: "",
                    plannedQuantity: null,
                    initialActualQuantity: null,
                    initialStockTransactionId: null,
                  })
                }
              >
                <PlusIcon data-icon="inline-start" />
                Thêm vật tư khác
              </Button>
            </div>

            {fields.length === 0 ? (
              <p className="text-sm text-muted-foreground">Chưa có vật tư nào được khai báo.</p>
            ) : (
              <div className="flex flex-col gap-3">
                {fields.map((fieldItem, index) => (
                  <ResultSupplyRow
                    key={fieldItem.id}
                    index={index}
                    control={form.control}
                    isLockedPlanned={fieldItem.plannedQuantity !== null}
                    hadStockTransaction={fieldItem.initialStockTransactionId !== null}
                    plannedQuantity={fieldItem.plannedQuantity}
                    fallbackName={initialSupplyNames.get(fieldItem.supplyId)}
                    onRemove={() => remove(index)}
                  />
                ))}
              </div>
            )}
          </section>
        )}

        {submitError !== null && <ErrorAlert error={submitError} />}

        <FormActionBar
          backTo={`/tasks/${task.id}`}
          isDirty={form.formState.isDirty}
          submitLabel={isWaitingForSupply ? "Chuyển Chờ vật tư" : isAlreadyDone ? "Lưu bổ sung" : "Lưu kết quả"}
          pending={recordMutation.isPending}
        />
      </form>

      <ReasonDialog
        open={reasonOpen}
        onOpenChange={(open) => {
          setReasonOpen(open);
          if (!open) {
            setReasonError(null);
          }
        }}
        title="Lý do sửa hoặc bỏ vật tư đã xuất kho"
        description="Thay đổi này sẽ hủy giao dịch xuất kho cũ của vật tư và tạo giao dịch mới nếu còn sử dụng."
        confirmLabel="Xác nhận lưu"
        pending={recordMutation.isPending}
        error={reasonError}
        onConfirm={(reason) => {
          if (pendingValues) {
            void executeSubmit(pendingValues, reason);
          }
        }}
      />

      <Dialog open={insufficientError !== null} onOpenChange={(open) => !open && setInsufficientError(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Không đủ tồn kho vật tư</DialogTitle>
            <DialogDescription>{insufficientError?.message}</DialogDescription>
          </DialogHeader>
          <p className="text-sm text-muted-foreground">Bạn muốn xử lý như thế nào?</p>
          <DialogFooter className="flex-col gap-2 sm:flex-col">
            {!isAlreadyDone && (
              <Button
                type="button"
                className="min-h-11 w-full"
                onClick={handleSwitchToWaitingForSupply}
                disabled={recordMutation.isPending}
              >
                Chuyển công việc sang Chờ bổ sung vật tư
              </Button>
            )}
            <Button type="button" variant="outline" className="min-h-11 w-full" onClick={handleAdjustQuantity}>
              Đổi lượng dùng
            </Button>
            <Button type="button" variant="outline" className="min-h-11 w-full" onClick={handlePickAnotherSupply}>
              Chọn vật tư khác
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}

interface ResultSupplyRowProps {
  index: number;
  control: Control<RecordResultFormValues>;
  isLockedPlanned: boolean;
  hadStockTransaction: boolean;
  plannedQuantity: number | null;
  fallbackName?: string;
  onRemove: () => void;
}

function ResultSupplyRow({
  index,
  control,
  isLockedPlanned,
  hadStockTransaction,
  plannedQuantity,
  fallbackName,
  onRemove,
}: ResultSupplyRowProps) {
  const supplyId = useWatch({ control, name: `supplies.${index}.supplyId` });
  const supplyQuery = useSupply(supplyId);
  const supply = supplyQuery.data;
  const lockedSupply = isLockedPlanned || hadStockTransaction;

  return (
    <div className="flex flex-col gap-3 rounded-md border p-3 sm:flex-row sm:items-start">
      <div className="flex-1">
        <Controller
          name={`supplies.${index}.supplyId`}
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={`result-supply-${index}`}>Vật tư</FieldLabel>
              {lockedSupply ? (
                <div id={`result-supply-${index}`} className="py-2 text-sm font-medium">
                  {supply?.name ?? fallbackName ?? "Vật tư"}
                </div>
              ) : (
                <SupplySelect
                  id={`result-supply-${index}`}
                  value={field.value}
                  onChange={field.onChange}
                  status="Active"
                  invalid={fieldState.invalid}
                />
              )}
              {supply && (
                <FieldDescription>
                  Tồn hiện tại: <QuantityText value={supply.stockQuantity} unit={supply.unit} />
                  {plannedQuantity !== null && (
                    <>
                      {" "}
                      · Dự kiến: <QuantityText value={plannedQuantity} unit={supply.unit} />
                    </>
                  )}
                </FieldDescription>
              )}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </div>

      <div className="w-full sm:w-44">
        <Controller
          name={`supplies.${index}.actualQuantity`}
          control={control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor={`result-supply-qty-${index}`}>Lượng thực dùng</FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id={`result-supply-qty-${index}`}
                  inputMode="decimal"
                  autoComplete="off"
                  placeholder="Để trống nếu 0"
                  aria-invalid={fieldState.invalid}
                />
                {supply && <span className="shrink-0 text-sm text-muted-foreground">{unitLabels[supply.unit]}</span>}
              </div>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </div>

      {!isLockedPlanned && (
        <Button
          type="button"
          variant="ghost"
          size="icon"
          className="min-h-11 min-w-11 self-end text-destructive sm:mt-6"
          onClick={onRemove}
          aria-label="Xóa dòng vật tư"
        >
          <Trash2Icon />
        </Button>
      )}
    </div>
  );
}
