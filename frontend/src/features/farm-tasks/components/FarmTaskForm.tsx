import { zodResolver } from "@hookform/resolvers/zod";
import { PlusIcon, Trash2Icon } from "lucide-react";
import { type ReactNode, useCallback, useState } from "react";
import { Controller, useFieldArray, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { useFarmTaskAssignees } from "@/features/farm-tasks/queries";
import { type FarmTaskFormOutput, farmTaskFormSchema, type FarmTaskFormValues } from "@/features/farm-tasks/schemas";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import type { SeasonOptionItem } from "@/features/seasons/types";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import { farmTaskTypeLabels, userRoleLabels } from "@/lib/enum-labels";
import { farmTaskTypes } from "@/lib/enums";
import { formatDate } from "@/lib/format";

export type FarmTaskFormSubmit = (
  values: FarmTaskFormOutput,
  setError: UseFormSetError<FarmTaskFormValues>,
) => Promise<{ error: unknown } | void>;

interface FarmTaskFormProps {
  mode: "create" | "edit";
  defaultValues: FarmTaskFormValues;
  onSubmit: FarmTaskFormSubmit;
  pending: boolean;
  backTo: string;
  submitLabel: string;
  seasonFallbackLabel?: string;
  notice?: ReactNode;
}

export function FarmTaskForm({
  mode,
  defaultValues,
  onSubmit,
  pending,
  backTo,
  submitLabel,
  seasonFallbackLabel,
  notice,
}: FarmTaskFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [selectedSeason, setSelectedSeason] = useState<SeasonOptionItem | undefined>(undefined);

  const form = useForm<FarmTaskFormValues, unknown, FarmTaskFormOutput>({
    resolver: zodResolver(farmTaskFormSchema()),
    defaultValues,
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: "supplies",
  });

  const seasonId = useWatch({ control: form.control, name: "seasonId" });
  const taskType = useWatch({ control: form.control, name: "type" });
  const isRecurring = useWatch({ control: form.control, name: "isRecurring" });

  const assigneesQuery = useFarmTaskAssignees(seasonId || undefined, seasonId !== "");
  const assignees = assigneesQuery.data ?? [];

  const handleSeasonChange = useCallback(
    (nextSeasonId: string, option?: SeasonOptionItem) => {
      const prevSeasonId = form.getValues("seasonId");
      form.setValue("seasonId", nextSeasonId, {
        shouldValidate: nextSeasonId !== "",
        shouldDirty: prevSeasonId !== "" || nextSeasonId !== defaultValues.seasonId,
      });
      setSelectedSeason(option);
      // Đổi mùa vụ thì xóa người thực hiện đã chọn nếu sang mùa vụ khác phạm vi phân công.
      if (prevSeasonId && prevSeasonId !== nextSeasonId) {
        form.setValue("assigneeId", "");
      }
    },
    [defaultValues.seasonId, form],
  );

  async function submit(values: FarmTaskFormOutput) {
    setSubmitError(null);
    const result = await onSubmit(values, form.setError);
    if (result) {
      setSubmitError(result.error);
    }
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
      <FieldGroup>
        <Controller
          name="seasonId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="task-season">
                Mùa vụ
                <RequiredMark />
              </FieldLabel>
              {mode === "edit" && seasonFallbackLabel ? (
                <Input id="task-season" value={seasonFallbackLabel} disabled readOnly />
              ) : (
                <SeasonSelect
                  id="task-season"
                  value={field.value}
                  onChange={handleSeasonChange}
                  operation="ManageFarmTasks"
                  invalid={fieldState.invalid}
                  disabled={mode === "edit"}
                  allowInactiveFallback={mode === "edit"}
                />
              )}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="type"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="task-type">
                Loại công việc
                <RequiredMark />
              </FieldLabel>
              <NativeSelect {...field} id="task-type" className="w-full" required aria-invalid={fieldState.invalid}>
                {farmTaskTypes.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {farmTaskTypeLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {taskType === "Other" && (
          <Controller
            name="typeOther"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="task-type-other">
                  Tên loại công việc
                  <RequiredMark />
                </FieldLabel>
                <Input
                  {...field}
                  id="task-type-other"
                  placeholder="Ví dụ: Tỉa cành, che phủ rơm"
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        {mode === "create" && (
          <Controller
            name="isRecurring"
            control={form.control}
            render={({ field }) => (
              <label className="flex min-h-11 cursor-pointer items-start gap-3 rounded-lg border bg-card p-3">
                <input
                  type="checkbox"
                  checked={field.value}
                  onChange={(e) => field.onChange(e.target.checked)}
                  className="mt-0.5 h-4 w-4 accent-primary"
                />
                <div className="space-y-0.5 text-sm">
                  <div className="font-medium">Lặp định kỳ đến hết mùa vụ</div>
                  <p className="text-muted-foreground">
                    Tự động sinh các công việc riêng lẻ cách đều nhau cho tới hết ngày dự kiến thu hoạch.
                  </p>
                </div>
              </label>
            )}
          />
        )}

        <div className="grid gap-4 sm:grid-cols-2">
          <Controller
            name="scheduledDate"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="task-scheduled-date">
                  {isRecurring ? "Ngày bắt đầu lặp" : "Ngày dự kiến"}
                  <RequiredMark />
                </FieldLabel>
                <Input {...field} id="task-scheduled-date" type="date" required aria-invalid={fieldState.invalid} />
                {isRecurring && selectedSeason && (
                  <FieldDescription>
                    Sinh việc đến hết ngày dự kiến thu hoạch ({formatDate(selectedSeason.expectedHarvestDate)}).
                  </FieldDescription>
                )}
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          {isRecurring ? (
            <Controller
              name="intervalDays"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="task-interval-days">
                    Chu kỳ lặp (ngày)
                    <RequiredMark />
                  </FieldLabel>
                  <Input
                    {...field}
                    id="task-interval-days"
                    type="number"
                    min={1}
                    inputMode="numeric"
                    aria-invalid={fieldState.invalid}
                  />
                  <FieldError errors={[fieldState.error]} />
                </Field>
              )}
            />
          ) : (
            <Controller
              name="plannedDurationMinutes"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="task-planned-duration">Thời lượng dự kiến (phút)</FieldLabel>
                  <Input
                    {...field}
                    id="task-planned-duration"
                    type="number"
                    min={1}
                    inputMode="numeric"
                    placeholder="Ví dụ: 60"
                    aria-invalid={fieldState.invalid}
                  />
                  <FieldError errors={[fieldState.error]} />
                </Field>
              )}
            />
          )}
        </div>

        {isRecurring && (
          <Controller
            name="plannedDurationMinutes"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="task-recurring-duration">Thời lượng dự kiến mỗi lần (phút)</FieldLabel>
                <Input
                  {...field}
                  id="task-recurring-duration"
                  type="number"
                  min={1}
                  inputMode="numeric"
                  placeholder="Ví dụ: 45"
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        <Controller
          name="assigneeId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="task-assignee">Người thực hiện</FieldLabel>
              <NativeSelect
                {...field}
                id="task-assignee"
                className="w-full"
                disabled={seasonId === ""}
                aria-invalid={fieldState.invalid}
              >
                <NativeSelectOption value="">Chưa giao người</NativeSelectOption>
                {assignees.map((member) => (
                  <NativeSelectOption key={member.userId} value={member.userId}>
                    {`${member.fullName} (${userRoleLabels[member.role]})`}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldDescription>
                Chỉ Chủ hộ và thành viên, nhân công đã được phân công vào mùa vụ mới có thể nhận việc.
              </FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <div className="space-y-3 rounded-lg border p-4">
          <div className="flex flex-wrap items-center justify-between gap-2">
            <div>
              <h2 className="text-sm font-medium">Vật tư dự kiến dùng</h2>
              <p className="text-xs text-muted-foreground">Không bắt buộc. Chỉ dùng để tham khảo khi thực hiện.</p>
            </div>
            <Button
              type="button"
              variant="outline"
              size="sm"
              className="min-h-11"
              onClick={() => append({ supplyId: "", plannedQuantity: "" })}
            >
              <PlusIcon className="mr-1.5 h-4 w-4" />
              Thêm vật tư
            </Button>
          </div>

          {fields.length === 0 ? (
            <p className="text-sm text-muted-foreground">Chưa khai báo vật tư dự kiến.</p>
          ) : (
            <div className="flex flex-col gap-4">
              {fields.map((row, index) => (
                <div key={row.id} className="flex flex-col gap-3 rounded-md border bg-muted/20 p-3">
                  <div className="flex items-start justify-between gap-2">
                    <span className="text-xs font-medium text-muted-foreground">Vật tư #{index + 1}</span>
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon"
                      aria-label={`Xóa dòng vật tư ${index + 1}`}
                      className="h-9 w-9 text-destructive hover:text-destructive"
                      onClick={() => remove(index)}
                    >
                      <Trash2Icon className="h-4 w-4" />
                    </Button>
                  </div>

                  <Controller
                    name={`supplies.${index}.supplyId`}
                    control={form.control}
                    render={({ field, fieldState }) => (
                      <Field data-invalid={fieldState.invalid}>
                        <FieldLabel htmlFor={`task-supply-${index}`}>
                          Vật tư
                          <RequiredMark />
                        </FieldLabel>
                        <SupplySelect
                          id={`task-supply-${index}`}
                          value={field.value}
                          onChange={field.onChange}
                          status="Active"
                          invalid={fieldState.invalid}
                        />
                        <FieldError errors={[fieldState.error]} />
                      </Field>
                    )}
                  />

                  <Controller
                    name={`supplies.${index}.plannedQuantity`}
                    control={form.control}
                    render={({ field, fieldState }) => (
                      <Field data-invalid={fieldState.invalid}>
                        <FieldLabel htmlFor={`task-supply-qty-${index}`}>
                          Lượng dự kiến
                          <RequiredMark />
                        </FieldLabel>
                        <Input
                          {...field}
                          id={`task-supply-qty-${index}`}
                          inputMode="decimal"
                          autoComplete="off"
                          placeholder="Ví dụ: 5 hoặc 2,5"
                          aria-invalid={fieldState.invalid}
                        />
                        <FieldError errors={[fieldState.error]} />
                      </Field>
                    )}
                  />
                </div>
              ))}
            </div>
          )}
        </div>

        <Controller
          name="note"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="task-note">Ghi chú</FieldLabel>
              <Textarea
                {...field}
                id="task-note"
                rows={3}
                placeholder="Lưu ý thêm cho người thực hiện"
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {submitError !== null && <ErrorAlert error={submitError} />}
      {notice}

      <FormActionBar
        backTo={backTo}
        isDirty={form.formState.isDirty}
        submitLabel={submitLabel}
        pending={pending}
      />
    </form>
  );
}

function RequiredMark() {
  return (
    <span aria-hidden className="text-destructive">
      *
    </span>
  );
}
