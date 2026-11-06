import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useState } from "react";
import { Controller, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { type PlotFormOutput, plotFormSchema, type PlotFormValues } from "@/features/plots/schemas";
import { recordStatusLabels, soilTypeLabels, waterSourceLabels } from "@/lib/enum-labels";
import { recordStatuses, soilTypes, waterSources } from "@/lib/enums";

/** Kết quả gửi form: có lỗi thì form hiện lỗi đó và giữ nguyên dữ liệu đã nhập (NFR-09). */
export type PlotFormSubmit = (
  values: PlotFormOutput,
  setError: UseFormSetError<PlotFormValues>,
) => Promise<{ error: unknown } | void>;

interface PlotFormProps {
  mode: "create" | "edit";
  defaultValues: PlotFormValues;
  onSubmit: PlotFormSubmit;
  pending: boolean;
  backTo: string;
  /** Nội dung riêng của trang đặt trên nút lưu, ví dụ báo dữ liệu đã bị người khác thay đổi kèm nút tải lại. */
  notice?: ReactNode;
}

/** Form tạo, sửa thửa đất (mẫu trang form): một cột, nhãn trên ô, ô bắt buộc có `*` đỏ, thanh nút dính đáy. */
export function PlotForm({ mode, defaultValues, onSubmit, pending, backTo, notice }: PlotFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<PlotFormValues, unknown, PlotFormOutput>({
    resolver: zodResolver(plotFormSchema(mode)),
    defaultValues,
  });
  // Theo dõi để hiện ô ghi chú khi chọn "Khác".
  const soilType = useWatch({ control: form.control, name: "soilType" });
  const waterSource = useWatch({ control: form.control, name: "waterSource" });

  async function submit(values: PlotFormOutput) {
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
              <FieldLabel htmlFor="plot-code">
                Mã thửa
                {mode === "edit" && <RequiredMark />}
              </FieldLabel>
              <Input {...field} id="plot-code" autoComplete="off" aria-invalid={fieldState.invalid} />
              {mode === "create" && <FieldDescription>Để trống thì hệ thống tự đặt mã, ví dụ TD001.</FieldDescription>}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="areaM2"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="plot-area">
                Diện tích
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="plot-area"
                  inputMode="decimal"
                  autoComplete="off"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <span className="shrink-0 text-sm text-muted-foreground">m²</span>
              </div>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="locationDescription"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="plot-location">Vị trí</FieldLabel>
              <Input {...field} id="plot-location" placeholder="Ví dụ: sau nhà, cạnh kênh" aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="soilType"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="plot-soil-type">
                Loại đất
                <RequiredMark />
              </FieldLabel>
              <NativeSelect {...field} id="plot-soil-type" className="w-full" required aria-invalid={fieldState.invalid}>
                <NativeSelectOption value="" disabled>
                  Chọn loại đất
                </NativeSelectOption>
                {soilTypes.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {soilTypeLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {/* Chọn "Khác" thì có ô ghi chú tự do đi kèm. */}
        {soilType === "Other" && (
          <Controller
            name="soilTypeOther"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="plot-soil-type-other">Loại đất khác</FieldLabel>
                <Input {...field} id="plot-soil-type-other" aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        <Controller
          name="waterSource"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="plot-water-source">
                Nguồn nước
                <RequiredMark />
              </FieldLabel>
              <NativeSelect {...field} id="plot-water-source" className="w-full" required aria-invalid={fieldState.invalid}>
                <NativeSelectOption value="" disabled>
                  Chọn nguồn nước
                </NativeSelectOption>
                {waterSources.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {waterSourceLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {waterSource === "Other" && (
          <Controller
            name="waterSourceOther"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="plot-water-source-other">Nguồn nước khác</FieldLabel>
                <Input {...field} id="plot-water-source-other" aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        {/* FR-PLOT-02: thửa không xóa được, chỉ chuyển sang ngừng sử dụng khi sửa. */}
        {mode === "edit" && (
          <Controller
            name="status"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="plot-status">
                  Tình trạng sử dụng
                  <RequiredMark />
                </FieldLabel>
                <NativeSelect {...field} id="plot-status" className="w-full" required aria-invalid={fieldState.invalid}>
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
              <FieldLabel htmlFor="plot-note">Ghi chú</FieldLabel>
              <Textarea {...field} id="plot-note" rows={3} aria-invalid={fieldState.invalid} />
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
        submitLabel="Lưu thửa đất"
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
