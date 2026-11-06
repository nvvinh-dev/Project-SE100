import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useState } from "react";
import { Controller, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { type SupplyFormOutput, supplyFormSchema, type SupplyFormValues } from "@/features/supplies/schemas";
import { recordStatusLabels, supplyGroupLabels, unitLabels } from "@/lib/enum-labels";
import { recordStatuses, supplyGroups, units, type Unit } from "@/lib/enums";

/** Kết quả gửi form: có lỗi thì form hiện lỗi đó và giữ nguyên dữ liệu đã nhập (NFR-09). */
export type SupplyFormSubmit = (
  values: SupplyFormOutput,
  setError: UseFormSetError<SupplyFormValues>,
) => Promise<{ error: unknown } | void>;

interface SupplyFormProps {
  mode: "create" | "edit";
  defaultValues: SupplyFormValues;
  /** Vật tư đã có giao dịch kho: khóa ô đơn vị (BR-CAT-05). */
  unitLocked?: boolean;
  onSubmit: SupplyFormSubmit;
  pending: boolean;
  backTo: string;
  /** Nội dung riêng của trang đặt trên nút lưu, ví dụ báo dữ liệu đã bị người khác thay đổi kèm nút tải lại. */
  notice?: ReactNode;
}

/** Form tạo, sửa vật tư. Không có ô tồn và giá vốn: tồn đổi qua giao dịch kho, giá vốn do hệ thống tính (FR-SUP-02). */
export function SupplyForm({ mode, defaultValues, unitLocked = false, onSubmit, pending, backTo, notice }: SupplyFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<SupplyFormValues, unknown, SupplyFormOutput>({
    resolver: zodResolver(supplyFormSchema(mode)),
    defaultValues,
  });
  // Hiện đơn vị đang chọn sau ô ngưỡng.
  const unit = useWatch({ control: form.control, name: "unit" });

  async function submit(values: SupplyFormOutput) {
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
          name="code"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="supply-code">
                Mã vật tư
                {mode === "edit" && <RequiredMark />}
              </FieldLabel>
              <Input {...field} id="supply-code" autoComplete="off" aria-invalid={fieldState.invalid} />
              {mode === "create" && <FieldDescription>Để trống thì hệ thống tự đặt mã, ví dụ VT001.</FieldDescription>}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="name"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="supply-name">
                Tên vật tư
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="supply-name" autoComplete="off" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="group"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="supply-group">
                Nhóm vật tư
                <RequiredMark />
              </FieldLabel>
              <NativeSelect {...field} id="supply-group" className="w-full" required aria-invalid={fieldState.invalid}>
                <NativeSelectOption value="" disabled>
                  Chọn nhóm vật tư
                </NativeSelectOption>
                {supplyGroups.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {supplyGroupLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="unit"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="supply-unit">
                Đơn vị tính
                <RequiredMark />
              </FieldLabel>
              <NativeSelect
                {...field}
                id="supply-unit"
                className="w-full"
                required
                disabled={unitLocked}
                aria-invalid={fieldState.invalid}
              >
                <NativeSelectOption value="" disabled>
                  Chọn đơn vị tính
                </NativeSelectOption>
                {units.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {unitLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              {unitLocked && <FieldDescription>Vật tư đã có giao dịch kho nên không đổi được đơn vị.</FieldDescription>}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="minStockLevel"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="supply-min-stock">Ngưỡng tồn tối thiểu</FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="supply-min-stock"
                  inputMode="decimal"
                  autoComplete="off"
                  aria-invalid={fieldState.invalid}
                />
                {isUnit(unit) && <span className="shrink-0 text-sm text-muted-foreground">{unitLabels[unit]}</span>}
              </div>
              <FieldDescription>Tồn thấp hơn mức này thì hệ thống cảnh báo. Để trống là 0.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="referencePrice"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="supply-reference-price">Giá nhập tham khảo</FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="supply-reference-price"
                  inputMode="decimal"
                  autoComplete="off"
                  aria-invalid={fieldState.invalid}
                />
                <span className="shrink-0 text-sm text-muted-foreground">₫</span>
              </div>
              <FieldDescription>Chỉ để gợi ý khi nhập kho, không dùng để tính chi phí.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {/* Vật tư không xóa được, chỉ chuyển sang ngừng sử dụng khi sửa. */}
        {mode === "edit" && (
          <Controller
            name="status"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="supply-status">
                  Trạng thái
                  <RequiredMark />
                </FieldLabel>
                <NativeSelect {...field} id="supply-status" className="w-full" required aria-invalid={fieldState.invalid}>
                  {recordStatuses.map((value) => (
                    <NativeSelectOption key={value} value={value}>
                      {recordStatusLabels[value]}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
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
              <FieldLabel htmlFor="supply-note">Ghi chú</FieldLabel>
              <Textarea {...field} id="supply-note" rows={3} aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {submitError !== null && <ErrorAlert error={submitError} />}
      {notice}

      <FormActionBar backTo={backTo} isDirty={form.formState.isDirty} submitLabel="Lưu vật tư" pending={pending} />
    </form>
  );
}

function isUnit(value: string): value is Unit {
  return (units as readonly string[]).includes(value);
}

function RequiredMark() {
  return (
    <span aria-hidden className="text-destructive">
      *
    </span>
  );
}
