import { zodResolver } from "@hookform/resolvers/zod";
import { useCallback, useMemo, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate, useSearchParams } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { showSuccess } from "@/components/common/toast";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { useCreateProduceStockAdjustment, useProduceStockOverview } from "@/features/harvests/queries";
import {
  emptyProduceStockAdjustmentFormValues,
  type ProduceStockAdjustmentFormOutput,
  produceStockAdjustmentFormFields,
  produceStockAdjustmentFormSchema,
  type ProduceStockAdjustmentFormValues,
  toCreateProduceStockAdjustmentRequest,
} from "@/features/harvests/schemas";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import type { SeasonOptionItem } from "@/features/seasons/types";
import { todayInVietnam } from "@/features/supplies/schemas";
import { produceAdjustmentReasonLabels, qualityGradeLabels, unitLabels } from "@/lib/enum-labels";
import { produceAdjustmentReasons, qualityGrades } from "@/lib/enums";
import { formatQuantity, parseDecimalInput } from "@/lib/format";
import { setFieldErrors } from "@/lib/form-errors";
import { useRequestId } from "@/lib/request-id";

/**
 * `/produce-stock/adjust`: Chủ hộ điều chỉnh giảm tồn nông sản theo mùa vụ và loại chất lượng (FR-HAR-07).
 * Hỗ trợ `?seasonId=&grade=` khi mở từ trang Tồn nông sản.
 */
export function ProduceStockAdjustPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  // BR-GEN-10: sinh một lần khi mở form để chống trừ tồn hai lần khi gửi lại.
  const requestId = useRequestId();
  const adjustMutation = useCreateProduceStockAdjustment();

  const [selectedSeason, setSelectedSeason] = useState<SeasonOptionItem | undefined>(undefined);
  const [submitError, setSubmitError] = useState<unknown>(null);

  const defaults = useMemo(
    () =>
      emptyProduceStockAdjustmentFormValues({
        seasonId: searchParams.get("seasonId") ?? "",
        qualityGrade: searchParams.get("grade"),
      }),
    [searchParams],
  );

  const form = useForm<ProduceStockAdjustmentFormValues, unknown, ProduceStockAdjustmentFormOutput>({
    resolver: zodResolver(produceStockAdjustmentFormSchema),
    defaultValues: defaults,
  });

  const [seasonId, qualityGrade, reason, quantityText] = useWatch({
    control: form.control,
    name: ["seasonId", "qualityGrade", "reason", "quantity"],
  });

  const stockQuery = useProduceStockOverview({ seasonId }, Boolean(seasonId));
  const currentStockRow = stockQuery.data?.find((item) => item.qualityGrade === qualityGrade);
  const availableStock = currentStockRow?.quantity;
  const yieldUnit = currentStockRow?.yieldUnit ?? selectedSeason?.yieldUnit;

  const handleSeasonChange = useCallback(
    (nextSeasonId: string, option?: SeasonOptionItem) => {
      form.setValue("seasonId", nextSeasonId, { shouldDirty: true, shouldValidate: true });
      setSelectedSeason(option);
    },
    [form],
  );

  const parsedQuantity = parseDecimalInput(quantityText);
  const earlyExceedsStock =
    availableStock !== undefined && parsedQuantity !== null && parsedQuantity > availableStock;

  const formatAvailable = (qty: number) =>
    yieldUnit ? formatQuantity(qty, yieldUnit) : qty.toLocaleString("vi-VN", { maximumFractionDigits: 3 });

  async function submit(values: ProduceStockAdjustmentFormOutput) {
    setSubmitError(null);
    if (availableStock !== undefined && (parseDecimalInput(values.quantity) ?? 0) > availableStock) {
      form.setError("quantity", {
        type: "manual",
        message: `Lượng điều chỉnh vượt tồn hiện có (${formatAvailable(availableStock)}).`,
      });
      return;
    }

    try {
      const saved = await adjustMutation.mutateAsync(toCreateProduceStockAdjustmentRequest(values, requestId));
      showSuccess("Đã điều chỉnh giảm tồn nông sản.");
      void navigate(`/produce-stock?seasonId=${saved.seasonId}`, { replace: true });
    } catch (error) {
      if (!setFieldErrors(error, form.setError, produceStockAdjustmentFormFields)) {
        setSubmitError(error);
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader
        title="Điều chỉnh giảm tồn nông sản"
        description="Ghi giảm tồn do hao hụt, hư hỏng, sử dụng nội bộ hoặc để giống."
        backTo="/produce-stock"
      />

      <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
        <FieldGroup>
          <Controller
            name="seasonId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="adjust-season">
                  Mùa vụ <span className="text-destructive">*</span>
                </FieldLabel>
                <SeasonSelect
                  id="adjust-season"
                  value={field.value}
                  onChange={handleSeasonChange}
                  operation="AdjustProduceStock"
                  invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="qualityGrade"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="adjust-grade">
                  Loại chất lượng <span className="text-destructive">*</span>
                </FieldLabel>
                <NativeSelect
                  id="adjust-grade"
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
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

          {seasonId && (
            <div className="flex items-center justify-between rounded-lg border bg-muted/40 px-4 py-3 text-sm">
              <span className="text-muted-foreground">Tồn hiện có</span>
              <span className="font-semibold">
                {stockQuery.isPending ? (
                  "Đang tính..."
                ) : currentStockRow ? (
                  <QuantityText value={currentStockRow.quantity} unit={currentStockRow.yieldUnit} />
                ) : (
                  "—"
                )}
              </span>
            </div>
          )}

          <Controller
            name="quantity"
            control={form.control}
            render={({ field, fieldState }) => {
              const displayError =
                fieldState.error ??
                (earlyExceedsStock && availableStock !== undefined
                  ? {
                      message: `Lượng điều chỉnh vượt tồn hiện có (${formatAvailable(availableStock)}).`,
                    }
                  : undefined);
              return (
                <Field data-invalid={Boolean(displayError)}>
                  <FieldLabel htmlFor="adjust-quantity">
                    Lượng điều chỉnh giảm <span className="text-destructive">*</span>
                  </FieldLabel>
                  <div className="flex items-center gap-2">
                    <Input
                      {...field}
                      id="adjust-quantity"
                      inputMode="decimal"
                      placeholder="0"
                      aria-invalid={Boolean(displayError)}
                      className="flex-1"
                    />
                    {yieldUnit && (
                      <span className="min-w-12 rounded-md border bg-muted/40 px-3 py-2 text-center text-sm font-medium text-muted-foreground">
                        {unitLabels[yieldUnit]}
                      </span>
                    )}
                  </div>
                  <FieldDescription>Lượng giảm luôn là số dương và không vượt tồn hiện có.</FieldDescription>
                  <FieldError errors={[displayError]} />
                </Field>
              );
            }}
          />

          <Controller
            name="adjustmentDate"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="adjust-date">
                  Ngày điều chỉnh <span className="text-destructive">*</span>
                </FieldLabel>
                <Input
                  {...field}
                  id="adjust-date"
                  type="date"
                  max={todayInVietnam()}
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="reason"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="adjust-reason">
                  Lý do điều chỉnh <span className="text-destructive">*</span>
                </FieldLabel>
                <NativeSelect
                  id="adjust-reason"
                  value={field.value}
                  onChange={field.onChange}
                  onBlur={field.onBlur}
                  aria-invalid={fieldState.invalid}
                >
                  {produceAdjustmentReasons.map((item) => (
                    <NativeSelectOption key={item} value={item}>
                      {produceAdjustmentReasonLabels[item]}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          {reason === "Other" && (
            <Controller
              name="reasonOther"
              control={form.control}
              render={({ field, fieldState }) => (
                <Field data-invalid={fieldState.invalid}>
                  <FieldLabel htmlFor="adjust-reason-other">
                    Lý do cụ thể <span className="text-destructive">*</span>
                  </FieldLabel>
                  <Input
                    {...field}
                    id="adjust-reason-other"
                    placeholder="Nhập lý do điều chỉnh"
                    aria-invalid={fieldState.invalid}
                  />
                  <FieldError errors={[fieldState.error]} />
                </Field>
              )}
            />
          )}

          <Controller
            name="note"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="adjust-note">Ghi chú</FieldLabel>
                <Textarea
                  {...field}
                  id="adjust-note"
                  rows={3}
                  placeholder="Ghi chú thêm (không bắt buộc)"
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {submitError ? <ErrorAlert error={submitError} /> : null}

        <FormActionBar
          submitLabel="Điều chỉnh tồn"
          backTo="/produce-stock"
          pending={adjustMutation.isPending}
          isDirty={form.formState.isDirty}
        />
      </form>
    </div>
  );
}
