import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate, useSearchParams } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { showSuccess, showWarnings } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import { useCreateStockTransaction, useSupply } from "@/features/supplies/queries";
import {
  type StockAdjustFormOutput,
  stockAdjustFormSchema,
  type StockAdjustFormValues,
  todayInVietnam,
  toStockAdjustRequest,
} from "@/features/supplies/schemas";
import { unitLabels } from "@/lib/enum-labels";
import { setFieldErrors } from "@/lib/form-errors";
import { useRequestId } from "@/lib/request-id";

const formFields = ["supplyId", "quantity", "reason", "transactionDate", "note"] as const;

/** `/stock/adjust`: điều chỉnh tồn sau kiểm kê (Chủ hộ, TV+Kho). Nhận `?supplyId=` để chọn sẵn vật tư. */
export function StockAdjustPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const requestId = useRequestId();
  const createTransaction = useCreateStockTransaction();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<StockAdjustFormValues, unknown, StockAdjustFormOutput>({
    resolver: zodResolver(stockAdjustFormSchema),
    defaultValues: {
      supplyId: searchParams.get("supplyId") ?? "",
      direction: "decrease",
      quantity: "",
      reason: "",
      transactionDate: todayInVietnam(),
      note: "",
    },
  });
  const supplyId = useWatch({ control: form.control, name: "supplyId" });
  const supply = useSupply(supplyId);
  const unit = supply.data?.unit;
  const backTo = supplyId ? `/supplies/${supplyId}` : "/supplies";

  async function submit(values: StockAdjustFormOutput) {
    setSubmitError(null);
    try {
      const { warnings } = await createTransaction.mutateAsync(toStockAdjustRequest(values, requestId));
      showSuccess("Đã điều chỉnh tồn.");
      showWarnings(warnings);
      void navigate(`/supplies/${values.supplyId}`, { replace: true });
    } catch (error) {
      // INSUFFICIENT_STOCK và các lỗi nghiệp vụ khác hiện nguyên văn trên form, giữ dữ liệu đã nhập.
      if (!setFieldErrors(error, form.setError, formFields)) {
        setSubmitError(error);
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Điều chỉnh tồn" backTo={backTo} />
      <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
        <FieldGroup>
          <Controller
            name="supplyId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-adjust-supply">
                  Vật tư
                  <RequiredMark />
                </FieldLabel>
                <SupplySelect
                  id="stock-adjust-supply"
                  value={field.value}
                  onChange={field.onChange}
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
            name="direction"
            control={form.control}
            render={({ field }) => (
              <Field>
                <FieldLabel>
                  Điều chỉnh
                  <RequiredMark />
                </FieldLabel>
                <div className="grid grid-cols-2 gap-2">
                  <Button
                    type="button"
                    variant={field.value === "decrease" ? "default" : "outline"}
                    aria-pressed={field.value === "decrease"}
                    onClick={() => field.onChange("decrease")}
                  >
                    Giảm
                  </Button>
                  <Button
                    type="button"
                    variant={field.value === "increase" ? "default" : "outline"}
                    aria-pressed={field.value === "increase"}
                    onClick={() => field.onChange("increase")}
                  >
                    Tăng
                  </Button>
                </div>
              </Field>
            )}
          />

          <Controller
            name="quantity"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-adjust-quantity">
                  Số lượng
                  <RequiredMark />
                </FieldLabel>
                <div className="flex items-center gap-2">
                  <Input
                    {...field}
                    id="stock-adjust-quantity"
                    inputMode="decimal"
                    autoComplete="off"
                    required
                    aria-invalid={fieldState.invalid}
                  />
                  {unit && <span className="shrink-0 text-sm text-muted-foreground">{unitLabels[unit]}</span>}
                </div>
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="reason"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-adjust-reason">
                  Lý do
                  <RequiredMark />
                </FieldLabel>
                <Input
                  {...field}
                  id="stock-adjust-reason"
                  placeholder="Ví dụ: kiểm kê thiếu 2 kg"
                  autoComplete="off"
                  required
                  aria-invalid={fieldState.invalid}
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
                <FieldLabel htmlFor="stock-adjust-date">
                  Ngày
                  <RequiredMark />
                </FieldLabel>
                <Input {...field} id="stock-adjust-date" type="date" required aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="note"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-adjust-note">Ghi chú</FieldLabel>
                <Textarea {...field} id="stock-adjust-note" rows={3} aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {submitError !== null && <ErrorAlert error={submitError} />}

        <FormActionBar
          backTo={backTo}
          isDirty={form.formState.isDirty}
          submitLabel="Điều chỉnh tồn"
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
