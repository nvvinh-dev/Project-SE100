import { zodResolver } from "@hookform/resolvers/zod";
import { useEffect, useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { useNavigate, useSearchParams } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess, showWarnings } from "@/components/common/toast";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import { useCreateStockTransaction, useSupply } from "@/features/supplies/queries";
import {
  type StockInFormOutput,
  stockInFormSchema,
  type StockInFormValues,
  todayInVietnam,
  toStockInRequest,
} from "@/features/supplies/schemas";
import { unitLabels } from "@/lib/enum-labels";
import { setFieldErrors } from "@/lib/form-errors";
import { formatMoney, parseDecimalInput } from "@/lib/format";
import { useRequestId } from "@/lib/request-id";

const formFields = ["supplyId", "quantity", "unitCost", "transactionDate", "note"] as const;

/** `/stock/in`: nhập kho (Chủ hộ, TV+Kho). Nhận `?supplyId=` để chọn sẵn vật tư. */
export function StockInPage() {
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const requestId = useRequestId();
  const createTransaction = useCreateStockTransaction();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<StockInFormValues, unknown, StockInFormOutput>({
    resolver: zodResolver(stockInFormSchema),
    defaultValues: {
      supplyId: searchParams.get("supplyId") ?? "",
      quantity: "",
      unitCost: "",
      transactionDate: todayInVietnam(),
      note: "",
    },
  });
  const supplyId = useWatch({ control: form.control, name: "supplyId" });
  const quantity = useWatch({ control: form.control, name: "quantity" });
  const unitCost = useWatch({ control: form.control, name: "unitCost" });
  const supply = useSupply(supplyId);
  const unit = supply.data?.unit;
  const quantityValue = parseDecimalInput(quantity);
  const unitCostValue = parseDecimalInput(unitCost);

  const suggestedPrice = supply.data?.referencePrice;

  // Điền sẵn giá tham khảo khi đã tải xong vật tư; người dùng đã gõ đơn giá thì giữ nguyên.
  useEffect(() => {
    if (suggestedPrice !== undefined && !form.getFieldState("unitCost").isDirty) {
      form.setValue("unitCost", String(suggestedPrice).replace(".", ","));
    }
  }, [form, suggestedPrice, supplyId]);

  // Đơn giá gõ tay trước đó thuộc vật tư cũ nên bỏ đi khi đổi vật tư.
  function selectSupply(id: string) {
    form.setValue("supplyId", id, { shouldDirty: true, shouldValidate: true });
    form.resetField("unitCost", { defaultValue: "" });
  }

  async function submit(values: StockInFormOutput) {
    setSubmitError(null);
    try {
      const { warnings } = await createTransaction.mutateAsync(toStockInRequest(values, requestId));
      showSuccess("Đã nhập kho.");
      showWarnings(warnings);
      void navigate(`/supplies/${values.supplyId}`, { replace: true });
    } catch (error) {
      if (!setFieldErrors(error, form.setError, formFields)) {
        setSubmitError(error);
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Nhập kho" backTo="/supplies" />
      <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-6">
        <FieldGroup>
          <Controller
            name="supplyId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-in-supply">
                  Vật tư
                  <RequiredMark />
                </FieldLabel>
                <SupplySelect
                  id="stock-in-supply"
                  value={field.value}
                  onChange={selectSupply}
                  status="Active"
                  invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="quantity"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-in-quantity">
                  Số lượng
                  <RequiredMark />
                </FieldLabel>
                <div className="flex items-center gap-2">
                  <Input
                    {...field}
                    id="stock-in-quantity"
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
            name="unitCost"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-in-unit-cost">
                  Đơn giá nhập
                  <RequiredMark />
                </FieldLabel>
                <div className="flex items-center gap-2">
                  <Input
                    {...field}
                    id="stock-in-unit-cost"
                    inputMode="decimal"
                    autoComplete="off"
                    required
                    aria-invalid={fieldState.invalid}
                  />
                  <span className="shrink-0 text-sm text-muted-foreground">₫</span>
                </div>
                {quantityValue !== null && unitCostValue !== null && (
                  <FieldDescription>Thành tiền: {formatMoney(quantityValue * unitCostValue)}</FieldDescription>
                )}
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="transactionDate"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-in-date">
                  Ngày nhập
                  <RequiredMark />
                </FieldLabel>
                <Input {...field} id="stock-in-date" type="date" required aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="note"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="stock-in-note">Ghi chú</FieldLabel>
                <Textarea {...field} id="stock-in-note" rows={3} aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {submitError !== null && <ErrorAlert error={submitError} />}

        <FormActionBar
          backTo="/supplies"
          isDirty={form.formState.isDirty}
          submitLabel="Nhập kho"
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
