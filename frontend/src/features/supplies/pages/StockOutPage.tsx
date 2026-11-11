import { zodResolver } from "@hookform/resolvers/zod";
import { useCallback, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate, useSearchParams } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { showSuccess, showWarnings } from "@/components/common/toast";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import { useCreateStockTransaction, useSupply } from "@/features/supplies/queries";
import {
  type StockOutFormOutput,
  stockOutFormSchema,
  type StockOutFormValues,
  todayInVietnam,
  toStockOutRequest,
} from "@/features/supplies/schemas";
import { unitLabels } from "@/lib/enum-labels";
import { setFieldErrors } from "@/lib/form-errors";
import { formatMoney, parseDecimalInput } from "@/lib/format";
import { useRequestId } from "@/lib/request-id";

const formFields = ["supplyId", "seasonId", "quantity", "transactionDate", "note"] as const;

/**
 * `/stock/out`: xuất kho thủ công cho mùa vụ (Chủ hộ, TV+Kho). Nhận `?supplyId=` để chọn sẵn vật tư;
 * `?seasonId=` do ô chọn mùa vụ tự nhận khi mùa vụ đó đang canh tác.
 */
export function StockOutPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const requestId = useRequestId();
  const createTransaction = useCreateStockTransaction();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<StockOutFormValues, unknown, StockOutFormOutput>({
    resolver: zodResolver(stockOutFormSchema),
    defaultValues: {
      supplyId: searchParams.get("supplyId") ?? "",
      seasonId: "",
      quantity: "",
      transactionDate: todayInVietnam(),
      note: "",
    },
  });
  const supplyId = useWatch({ control: form.control, name: "supplyId" });
  const quantity = useWatch({ control: form.control, name: "quantity" });
  const supply = useSupply(supplyId);
  const unit = supply.data?.unit;
  const quantityValue = parseDecimalInput(quantity);
  const averageUnitCost = supply.data?.averageUnitCost;
  const backTo = supplyId ? `/supplies/${supplyId}` : "/supplies";

  // Ô chọn mùa vụ gọi lại hàm này trong effect khi nhận `?seasonId=`, nên giữ nguyên tham chiếu giữa các lần render.
  const selectSeason = useCallback(
    (seasonId: string) => form.setValue("seasonId", seasonId, { shouldDirty: true, shouldValidate: true }),
    [form],
  );

  async function submit(values: StockOutFormOutput) {
    setSubmitError(null);
    try {
      const { warnings } = await createTransaction.mutateAsync(toStockOutRequest(values, requestId));
      showSuccess("Đã xuất kho.");
      showWarnings(warnings);
      void navigate(`/supplies/${values.supplyId}`, { replace: true });
    } catch (error) {
      // INSUFFICIENT_STOCK, SEASON_INVALID_STATE hiện nguyên văn trên form, giữ dữ liệu đã nhập.
      if (!setFieldErrors(error, form.setError, formFields)) {
        setSubmitError(error);
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Xuất kho" backTo={backTo} />
      <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
        <FieldGroup>
          <Controller
            name="supplyId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-out-supply">
                  Vật tư
                  <RequiredMark />
                </FieldLabel>
                <SupplySelect
                  id="stock-out-supply"
                  value={field.value}
                  onChange={field.onChange}
                  status="Active"
                  invalid={fieldState.invalid}
                />
                {supply.data && (
                  <FieldDescription>
                    Tồn hiện tại: <QuantityText value={supply.data.stockQuantity} unit={supply.data.unit} />
                  </FieldDescription>
                )}
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="quantity"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-out-quantity">
                  Số lượng
                  <RequiredMark />
                </FieldLabel>
                <div className="flex items-center gap-2">
                  <Input
                    {...field}
                    id="stock-out-quantity"
                    inputMode="decimal"
                    autoComplete="off"
                    required
                    aria-invalid={fieldState.invalid}
                  />
                  {unit && <span className="shrink-0 text-sm text-muted-foreground">{unitLabels[unit]}</span>}
                </div>
                {/* BR-SUP-06: đơn giá xuất là giá vốn bình quân lúc ghi; số ở đây chỉ là dự kiến. */}
                {averageUnitCost !== undefined && (
                  <FieldDescription>
                    Đơn giá vốn: {formatMoney(averageUnitCost)}
                    {quantityValue !== null && <> · Thành tiền dự kiến: {formatMoney(quantityValue * averageUnitCost)}</>}
                  </FieldDescription>
                )}
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="seasonId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-out-season">
                  Mùa vụ
                  <RequiredMark />
                </FieldLabel>
                <SeasonSelect
                  id="stock-out-season"
                  value={field.value}
                  onChange={selectSeason}
                  operation="StockOut"
                  invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="transactionDate"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-out-date">
                  Ngày xuất
                  <RequiredMark />
                </FieldLabel>
                <Input {...field} id="stock-out-date" type="date" required aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="note"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-out-note">Ghi chú</FieldLabel>
                <Textarea {...field} id="stock-out-note" rows={3} aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {submitError !== null && <ErrorAlert error={submitError} />}

        <FormActionBar
          backTo={backTo}
          isDirty={form.formState.isDirty}
          submitLabel="Xuất kho"
          pending={createTransaction.isPending}
        />
      </form>
    </div>
  );
}

function RequiredMark() {
  return (
    <span aria-hidden className="text-destructive">
      *
    </span>
  );
}
