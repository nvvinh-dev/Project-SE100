import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useState } from "react";
import { Controller, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { CropSelect } from "@/features/seasons/components/CropSelect";
import { PlotSelect } from "@/features/seasons/components/PlotSelect";
import { useActiveCrops } from "@/features/seasons/queries";
import {
  type SeasonFormOutput,
  seasonFormSchema,
  type SeasonFormValues,
} from "@/features/seasons/schemas";
import type { CropOptionItem } from "@/features/seasons/types";
import { ApiError } from "@/lib/api-error";
import { unitLabels } from "@/lib/enum-labels";
import type { SeasonStatus } from "@/lib/enums";

export type SeasonFormSubmit = (
  values: SeasonFormOutput,
  setError: UseFormSetError<SeasonFormValues>,
) => Promise<{ error: unknown } | void>;

interface SeasonFormProps {
  mode: "create" | "edit";
  defaultValues: SeasonFormValues;
  onSubmit: SeasonFormSubmit;
  pending: boolean;
  backTo: string;
  seasonStatus?: SeasonStatus;
  selectedCropFallback?: { id: string; name: string; yieldUnit: string };
  notice?: ReactNode;
}

/** Form tạo và sửa mùa vụ (FR-SEA-01, FR-SEA-02, FR-SEA-03, FR-SEA-07). */
export function SeasonForm({
  mode,
  defaultValues,
  onSubmit,
  pending,
  backTo,
  seasonStatus,
  selectedCropFallback,
  notice,
}: SeasonFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [selectedCrop, setSelectedCrop] = useState<CropOptionItem | undefined>(undefined);
  const cropsQuery = useActiveCrops();

  const form = useForm<SeasonFormValues, unknown, SeasonFormOutput>({
    resolver: zodResolver(seasonFormSchema()),
    defaultValues,
  });

  const cropId = useWatch({ control: form.control, name: "cropId" });
  // Tìm đơn vị sản lượng của cây đang chọn
  const cropUnit =
    selectedCrop?.yieldUnit ??
    cropsQuery.data?.find((c) => c.id === cropId)?.yieldUnit ??
    (selectedCropFallback?.id === cropId ? selectedCropFallback.yieldUnit : undefined);

  // BR-SEA-10: Khi mùa vụ đang canh tác (InProgress), khóa đổi thửa và cây trồng
  const isLockedPlotAndCrop = mode === "edit" && seasonStatus === "InProgress";

  async function submit(values: SeasonFormOutput) {
    setSubmitError(null);
    const result = await onSubmit(values, form.setError);
    if (result) {
      setSubmitError(result.error);
    }
  }

  const isOverlapError =
    submitError instanceof ApiError &&
    (submitError.code === "SEASON_OVERLAP" || submitError.code === "SEASON_PLOT_BUSY");

  return (
    <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
      <FieldGroup>
        <Controller
          name="plotId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="season-plot">
                Thửa đất
                <RequiredMark />
              </FieldLabel>
              <PlotSelect
                id="season-plot"
                value={field.value}
                onChange={field.onChange}
                disabled={isLockedPlotAndCrop}
                invalid={fieldState.invalid}
              />
              {isLockedPlotAndCrop && (
                <FieldDescription>
                  Mùa vụ đang canh tác không thể thay đổi thửa đất (BR-SEA-10).
                </FieldDescription>
              )}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="cropId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="season-crop">
                Cây trồng
                <RequiredMark />
              </FieldLabel>
              <CropSelect
                id="season-crop"
                value={field.value}
                onChange={(newId, crop) => {
                  field.onChange(newId);
                  setSelectedCrop(crop);
                }}
                disabled={isLockedPlotAndCrop}
                invalid={fieldState.invalid}
                selectedCropFallback={selectedCropFallback}
              />
              {isLockedPlotAndCrop && (
                <FieldDescription>
                  Mùa vụ đang canh tác không thể thay đổi cây trồng (BR-SEA-10).
                </FieldDescription>
              )}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <div className="grid grid-cols-1 gap-6 sm:grid-cols-2">
          <Controller
            name="startDate"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="season-start-date">
                  Ngày bắt đầu
                  <RequiredMark />
                </FieldLabel>
                <Input
                  {...field}
                  id="season-start-date"
                  type="date"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="expectedHarvestDate"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="season-expected-harvest-date">
                  Ngày dự kiến thu hoạch
                  <RequiredMark />
                </FieldLabel>
                <Input
                  {...field}
                  id="season-expected-harvest-date"
                  type="date"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </div>

        <Controller
          name="targetYield"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="season-target-yield">
                Mục tiêu sản lượng
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="season-target-yield"
                  inputMode="decimal"
                  placeholder="Ví dụ: 4500 hoặc 4500,5"
                  autoComplete="off"
                  required
                  aria-invalid={fieldState.invalid}
                />
                {cropUnit && (
                  <span className="shrink-0 text-sm font-medium text-muted-foreground">
                    {unitLabels[cropUnit as keyof typeof unitLabels] ?? cropUnit}
                  </span>
                )}
              </div>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {submitError !== null && (
        <div className="flex flex-col gap-2">
          <ErrorAlert error={submitError} />
          {isOverlapError && (
            <Alert>
              <AlertDescription>
                Lưu ý: Nếu hộ có nhu cầu trồng xen nhiều mùa vụ trên cùng một thửa, Chủ hộ có thể bật tùy chọn
                &quot;Cho phép mùa vụ chồng lấn&quot; tại phần Cài đặt hộ.
              </AlertDescription>
            </Alert>
          )}
        </div>
      )}

      {notice}

      <FormActionBar
        backTo={backTo}
        isDirty={form.formState.isDirty}
        submitLabel="Lưu mùa vụ"
        pending={pending}
      />
    </form>
  );
}

function RequiredMark() {
  return (
    <span aria-hidden className="ml-1 text-destructive">
      *
    </span>
  );
}
