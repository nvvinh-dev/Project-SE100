import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useCallback, useState } from "react";
import { Controller, useForm, type UseFormSetError } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import {
  type HarvestFormOutput,
  harvestFormSchema,
  type HarvestFormValues,
} from "@/features/harvests/schemas";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import type { SeasonOptionItem } from "@/features/seasons/types";
import { todayInVietnam } from "@/features/supplies/schemas";
import { qualityGradeLabels, unitLabels } from "@/lib/enum-labels";
import { qualityGrades, type Unit } from "@/lib/enums";
import { formatDate } from "@/lib/format";

export type HarvestFormSubmit = (
  values: HarvestFormOutput,
  setError: UseFormSetError<HarvestFormValues>,
) => Promise<{ error: unknown } | void>;

interface HarvestFormProps {
  mode: "create" | "correct";
  defaultValues: HarvestFormValues;
  /** Thông tin mùa vụ cố định khi đính chính phiếu đã tạo (BR-HAR-03 không cho đổi mùa vụ). */
  fixedSeasonInfo?: {
    label: string;
    yieldUnit: Unit;
    startDate: string;
  };
  onSubmit: HarvestFormSubmit;
  pending: boolean;
  backTo: string;
  submitLabel: string;
  notice?: ReactNode;
}

/**
 * Form ghi nhận và đính chính phiếu thu hoạch (FR-HAR-01, FR-HAR-03).
 * Đơn vị tính tự động khóa theo đơn vị sản lượng của cây trồng trong mùa vụ.
 */
export function HarvestForm({
  mode,
  defaultValues,
  fixedSeasonInfo,
  onSubmit,
  pending,
  backTo,
  submitLabel,
  notice,
}: HarvestFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [selectedSeason, setSelectedSeason] = useState<SeasonOptionItem | undefined>(undefined);

  const form = useForm<HarvestFormValues, unknown, HarvestFormOutput>({
    resolver: zodResolver(harvestFormSchema),
    defaultValues,
  });

  const handleSeasonChange = useCallback(
    (seasonId: string, option?: SeasonOptionItem) => {
      form.setValue("seasonId", seasonId, { shouldDirty: true, shouldValidate: true });
      setSelectedSeason(option);
    },
    [form],
  );

  const effectiveYieldUnit = fixedSeasonInfo?.yieldUnit ?? selectedSeason?.yieldUnit;
  const effectiveStartDate = fixedSeasonInfo?.startDate ?? selectedSeason?.startDate;

  async function submit(values: HarvestFormOutput) {
    setSubmitError(null);
    // Kiểm tra sớm trước khi gọi API nếu đã biết ngày bắt đầu mùa vụ (BR-HAR-01).
    if (effectiveStartDate && values.harvestDate < effectiveStartDate) {
      form.setError("harvestDate", {
        type: "manual",
        message: `Ngày thu hoạch không được trước ngày bắt đầu mùa vụ (${formatDate(effectiveStartDate)}).`,
      });
      return;
    }

    const result = await onSubmit(values, form.setError);
    if (result) {
      setSubmitError(result.error);
    }
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
      <FieldGroup>
        {mode === "create" ? (
          <Controller
            name="seasonId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="harvest-season">
                  Mùa vụ
                  <RequiredMark />
                </FieldLabel>
                <SeasonSelect
                  id="harvest-season"
                  value={field.value}
                  onChange={handleSeasonChange}
                  operation="ManageHarvests"
                  invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        ) : (
          <Field>
            <FieldLabel>Mùa vụ</FieldLabel>
            <div className="flex min-h-10 items-center rounded-md border bg-muted/40 px-3 text-sm text-muted-foreground">
              {fixedSeasonInfo?.label ?? "—"}
            </div>
          </Field>
        )}

        <Controller
          name="harvestDate"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="harvest-date">
                Ngày thu hoạch
                <RequiredMark />
              </FieldLabel>
              <Input
                {...field}
                id="harvest-date"
                type="date"
                min={effectiveStartDate}
                max={todayInVietnam()}
                aria-invalid={fieldState.invalid}
              />
              {effectiveStartDate && (
                <FieldDescription>Từ ngày bắt đầu mùa vụ ({formatDate(effectiveStartDate)}) đến hôm nay.</FieldDescription>
              )}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="qualityGrade"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="harvest-quality-grade">
                Loại chất lượng
                <RequiredMark />
              </FieldLabel>
              <NativeSelect
                {...field}
                id="harvest-quality-grade"
                className="w-full"
                aria-invalid={fieldState.invalid}
              >
                {qualityGrades.map((grade) => (
                  <NativeSelectOption key={grade} value={grade}>
                    {qualityGradeLabels[grade]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="quantity"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="harvest-quantity">
                Sản lượng thực tế
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="harvest-quantity"
                  type="text"
                  inputMode="decimal"
                  placeholder="Ví dụ: 350 hoặc 12,5"
                  aria-invalid={fieldState.invalid}
                  className="flex-1"
                />
                <span className="flex h-10 min-w-16 items-center justify-center rounded-md border bg-muted/40 px-3 text-sm font-medium text-muted-foreground">
                  {effectiveYieldUnit ? unitLabels[effectiveYieldUnit] : "Đơn vị"}
                </span>
              </div>
              <FieldDescription>Đơn vị tính tự lấy theo cây trồng của mùa vụ, không đổi được.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="note"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="harvest-note">Ghi chú</FieldLabel>
              <Textarea
                {...field}
                id="harvest-note"
                rows={3}
                maxLength={500}
                placeholder="Ghi chú thêm về lô thu hoạch (không bắt buộc)"
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {notice}
      {submitError !== null && <ErrorAlert error={submitError} />}

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
    <span className="text-destructive" aria-hidden>
      {" "}
      *
    </span>
  );
}
