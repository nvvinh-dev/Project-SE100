import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useCallback, useState } from "react";
import { Controller, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { Link } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { MoneyText } from "@/components/common/MoneyText";
import { QuantityText } from "@/components/common/QuantityText";
import { buttonVariants } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { CustomerSelect } from "@/features/sales/components/CustomerSelect";
import { RequiredMark } from "@/features/sales/components/RequiredMark";
import { useProduceStock } from "@/features/sales/queries";
import { previewRevenue, type SaleFormOutput, saleFormSchema, type SaleFormValues } from "@/features/sales/schemas";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { qualityGradeLabels, salesChannelLabels, unitLabels } from "@/lib/enum-labels";
import { qualityGrades, salesChannels, type Unit } from "@/lib/enums";
import { parseDecimalInput } from "@/lib/format";

/** Kết quả gửi form: có lỗi thì form hiện lỗi đó và giữ nguyên dữ liệu đã nhập (NFR-09). */
export type SaleFormSubmit = (
  values: SaleFormOutput,
  setError: UseFormSetError<SaleFormValues>,
) => Promise<{ error: unknown } | void>;

/** Lượng của chính giao dịch đang điều chỉnh, được cộng lại vào tồn khi giữ nguyên mùa vụ và loại (BR-SAL-03). */
export interface StockCredit {
  seasonId: string;
  qualityGrade: string;
  quantity: number;
}

interface SaleFormProps {
  mode: "create" | "correct";
  defaultValues: SaleFormValues;
  /** Đơn vị sản lượng của cây trong mùa vụ đã chọn sẵn (form điều chỉnh). */
  defaultYieldUnit?: Unit;
  stockCredit?: StockCredit;
  onSubmit: SaleFormSubmit;
  pending: boolean;
  backTo: string;
  submitLabel: string;
  notice?: ReactNode;
}

/** Form tạo, điều chỉnh giao dịch bán (FR-SAL-01, FR-SAL-02, FR-SAL-05; phác thảo màn hình tạo giao dịch bán). */
export function SaleForm({
  mode,
  defaultValues,
  defaultYieldUnit,
  stockCredit,
  onSubmit,
  pending,
  backTo,
  submitLabel,
  notice,
}: SaleFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [yieldUnit, setYieldUnit] = useState<Unit | undefined>(defaultYieldUnit);
  const form = useForm<SaleFormValues, unknown, SaleFormOutput>({
    resolver: zodResolver(saleFormSchema),
    defaultValues,
  });
  const seasonId = useWatch({ control: form.control, name: "seasonId" });
  const qualityGrade = useWatch({ control: form.control, name: "qualityGrade" });
  const channel = useWatch({ control: form.control, name: "channel" });
  const quantityText = useWatch({ control: form.control, name: "quantity" });
  const unitPriceText = useWatch({ control: form.control, name: "unitPrice" });

  const stock = useProduceStock(seasonId);
  const available = availableStock(stock.data, seasonId, qualityGrade, stockCredit);
  const quantity = parseDecimalInput(quantityText.trim());
  const exceedsStock = available !== undefined && quantity !== null && quantity > available;
  const revenue = previewRevenue(quantityText, unitPriceText);
  const unitLabel = yieldUnit ? unitLabels[yieldUnit] : "đơn vị";

  // Ô chọn mùa vụ gọi lại hàm này trong effect khi nhận `?seasonId=`, nên giữ nguyên tham chiếu giữa các lần render.
  const selectSeason = useCallback(
    (id: string, option?: { yieldUnit: Unit }) => {
      form.setValue("seasonId", id, { shouldDirty: true, shouldValidate: true });
      // Phiếu thu liên kết phải cùng mùa vụ (BR-SAL-05): đổi mùa vụ thì bỏ các phiếu đã chọn.
      form.setValue("harvestRecordIds", []);
      if (option) {
        setYieldUnit(option.yieldUnit);
      }
    },
    [form],
  );

  async function submit(values: SaleFormOutput) {
    setSubmitError(null);
    // FR-SAL-02: báo ngay khi vượt tồn đã biết; backend vẫn kiểm tra lại.
    if (exceedsStock) {
      form.setError("quantity", { type: "stock", message: exceedsMessage(available, unitLabel) });
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
        <Controller
          name="seasonId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="sale-season">
                Mùa vụ
                <RequiredMark />
              </FieldLabel>
              {/* BR-USR-13: TV+TC chọn được cả mùa vụ ngoài phân công; chỉ mùa vụ đang canh tác (mục 4.3). */}
              <SeasonSelect
                id="sale-season"
                value={field.value}
                onChange={selectSeason}
                operation="ManageSales"
                invalid={fieldState.invalid}
                disabled={mode === "correct"}
                allowInactiveFallback={mode === "correct"}
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
              <FieldLabel htmlFor="sale-grade">
                Loại chất lượng
                <RequiredMark />
              </FieldLabel>
              <NativeSelect
                {...field}
                onChange={(event) => {
                  field.onChange(event);
                  // Phiếu thu liên kết phải cùng loại chất lượng (BR-SAL-05).
                  form.setValue("harvestRecordIds", []);
                }}
                id="sale-grade"
                className="w-full"
                required
                aria-invalid={fieldState.invalid}
              >
                <NativeSelectOption value="" disabled>
                  Chọn loại chất lượng
                </NativeSelectOption>
                {qualityGrades.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {qualityGradeLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <StockLine seasonId={seasonId} available={available} yieldUnit={yieldUnit} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="customerId"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="sale-customer">Khách hàng</FieldLabel>
              <CustomerSelect
                id="sale-customer"
                value={field.value}
                onChange={(customerId) => {
                  field.onChange(customerId);
                  // Có khách hàng thì lỗi "chọn khách hàng hoặc kênh bán" không còn đúng.
                  form.clearErrors("channel");
                }}
                invalid={fieldState.invalid}
                keepCurrent={mode === "correct" ? defaultValues.customerId : undefined}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="channel"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="sale-channel">Kênh bán</FieldLabel>
              <NativeSelect {...field} id="sale-channel" className="w-full" aria-invalid={fieldState.invalid}>
                <NativeSelectOption value="">Không chọn</NativeSelectOption>
                {salesChannels.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {salesChannelLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldDescription>Chọn ít nhất khách hàng hoặc kênh bán.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {channel === "Other" && (
          <Controller
            name="channelOther"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="sale-channel-other">Kênh bán khác</FieldLabel>
                <Input {...field} id="sale-channel-other" autoComplete="off" aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        <Controller
          name="quantity"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid || exceedsStock}>
              <FieldLabel htmlFor="sale-quantity">
                Số lượng
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="sale-quantity"
                  inputMode="decimal"
                  autoComplete="off"
                  required
                  aria-invalid={fieldState.invalid || exceedsStock}
                />
                <span className="shrink-0 text-sm text-muted-foreground">{unitLabel}</span>
              </div>
              <FieldError
                errors={[
                  fieldState.error ?? (exceedsStock ? { message: exceedsMessage(available, unitLabel) } : undefined),
                ]}
              />
            </Field>
          )}
        />

        <Controller
          name="unitPrice"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="sale-unit-price">
                Đơn giá
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="sale-unit-price"
                  inputMode="decimal"
                  autoComplete="off"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <span className="shrink-0 text-sm text-muted-foreground">₫/{unitLabel}</span>
              </div>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <div className="flex items-center justify-between rounded-lg bg-muted px-3 py-2">
          <span className="text-sm text-muted-foreground">Thành tiền</span>
          <span className="font-semibold">{revenue === null ? "—" : <MoneyText amount={revenue} />}</span>
        </div>

        <Controller
          name="saleDate"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="sale-date">
                Ngày bán
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="sale-date" type="date" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="note"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="sale-note">Ghi chú</FieldLabel>
              <Textarea {...field} id="sale-note" rows={3} aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {notice}
      {submitError !== null && <ErrorAlert error={submitError} />}

      <FormActionBar backTo={backTo} isDirty={form.formState.isDirty} submitLabel={submitLabel} pending={pending} />
    </form>
  );
}

// Tồn có thể bán của cặp (mùa vụ, loại); `undefined` khi chưa chọn đủ hoặc chưa đọc được tồn.
function availableStock(
  stock: { qualityGrade: string; quantity: number }[] | undefined,
  seasonId: string,
  qualityGrade: string,
  credit: StockCredit | undefined,
): number | undefined {
  if (!seasonId || !qualityGrade || !stock) {
    return undefined;
  }
  const quantity = stock.find((item) => item.qualityGrade === qualityGrade)?.quantity ?? 0;
  const sameSale = credit && credit.seasonId === seasonId && credit.qualityGrade === qualityGrade;
  return sameSale ? quantity + credit.quantity : quantity;
}

function exceedsMessage(available: number | undefined, unitLabel: string): string {
  return `Số lượng vượt tồn có thể bán (${available ?? 0} ${unitLabel}).`;
}

/** Dòng "Tồn có thể bán" dưới ô loại chất lượng; chưa có tồn thì hướng dẫn ghi thu hoạch trước (BR-SAL-02). */
function StockLine({
  seasonId,
  available,
  yieldUnit,
}: {
  seasonId: string;
  available: number | undefined;
  yieldUnit: Unit | undefined;
}) {
  if (available === undefined) {
    return null;
  }
  if (available <= 0) {
    return (
      <div className="flex flex-wrap items-center gap-2 text-sm">
        <span className="text-muted-foreground">Chưa có tồn loại này.</span>
        <Link
          to={`/harvests/new?seasonId=${seasonId}`}
          className={buttonVariants({ variant: "outline", size: "sm", className: "min-h-11" })}
        >
          Ghi thu hoạch trước
        </Link>
      </div>
    );
  }
  return (
    <p className="text-sm text-muted-foreground">
      Tồn có thể bán: {yieldUnit ? <QuantityText value={available} unit={yieldUnit} /> : available}
    </p>
  );
}
