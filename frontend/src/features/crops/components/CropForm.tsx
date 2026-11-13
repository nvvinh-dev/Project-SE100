import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useState } from "react";
import { Controller, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { type CropFormOutput, cropFormSchema, type CropFormValues } from "@/features/crops/schemas";
import { cropGroupLabels, recordStatusLabels, unitLabels } from "@/lib/enum-labels";
import { cropGroups, recordStatuses, units } from "@/lib/enums";

/** Kết quả gửi form: có lỗi thì form hiện lỗi đó và giữ nguyên dữ liệu đã nhập (NFR-09). */
export type CropFormSubmit = (
  values: CropFormOutput,
  setError: UseFormSetError<CropFormValues>,
) => Promise<{ error: unknown } | void>;

interface CropFormProps {
  mode: "create" | "edit";
  defaultValues: CropFormValues;
  onSubmit: CropFormSubmit;
  pending: boolean;
  backTo: string;
  /** Cây đã có phiếu thu hoạch: khóa ô đơn vị sản lượng (BR-CAT-05). */
  yieldUnitLocked?: boolean;
  /** Nội dung riêng của trang đặt trên nút lưu, ví dụ báo dữ liệu đã bị người khác thay đổi kèm nút tải lại. */
  notice?: ReactNode;
}

/** Form tạo, sửa cây trồng (mẫu trang form): một cột, nhãn trên ô, ô bắt buộc có `*` đỏ, thanh nút dính đáy. */
export function CropForm({ mode, defaultValues, onSubmit, pending, backTo, yieldUnitLocked = false, notice }: CropFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<CropFormValues, unknown, CropFormOutput>({
    resolver: zodResolver(cropFormSchema),
    defaultValues,
  });
  // Theo dõi để hiện ô ghi chú khi chọn "Khác".
  const group = useWatch({ control: form.control, name: "group" });

  async function submit(values: CropFormOutput) {
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
          name="name"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="crop-name">
                Tên cây trồng
                <RequiredMark />
              </FieldLabel>
              <Input
                {...field}
                id="crop-name"
                placeholder="Ví dụ: Lúa ST25"
                autoComplete="off"
                required
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="group"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="crop-group">
                Nhóm cây
                <RequiredMark />
              </FieldLabel>
              <NativeSelect {...field} id="crop-group" className="w-full" required aria-invalid={fieldState.invalid}>
                <NativeSelectOption value="" disabled>
                  Chọn nhóm cây
                </NativeSelectOption>
                {cropGroups.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {cropGroupLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {/* Chọn "Khác" thì có ô ghi chú tự do đi kèm. */}
        {group === "Other" && (
          <Controller
            name="groupOther"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="crop-group-other">Nhóm cây khác</FieldLabel>
                <Input {...field} id="crop-group-other" aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        <Controller
          name="yieldUnit"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="crop-yield-unit">
                Đơn vị sản lượng
                <RequiredMark />
              </FieldLabel>
              {/* Khóa trên ô chọn, không khóa trong form: giá trị đơn vị hiện tại vẫn được gửi khi lưu. */}
              <NativeSelect
                {...field}
                id="crop-yield-unit"
                className="w-full"
                required
                disabled={yieldUnitLocked}
                aria-invalid={fieldState.invalid}
              >
                <NativeSelectOption value="" disabled>
                  Chọn đơn vị sản lượng
                </NativeSelectOption>
                {units.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {unitLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              {yieldUnitLocked ? (
                <FieldDescription>Cây đã có phiếu thu hoạch nên không đổi được đơn vị sản lượng.</FieldDescription>
              ) : (
                <FieldDescription>Dùng cho phiếu thu hoạch, tồn nông sản và giao dịch bán của cây này.</FieldDescription>
              )}
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="referenceInfo"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="crop-reference-info">Thông tin tham khảo</FieldLabel>
              <Textarea
                {...field}
                id="crop-reference-info"
                rows={4}
                placeholder="Ví dụ: thời gian sinh trưởng, lịch bón phân, lưu ý khi chăm sóc"
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {/* FR-CROP-03: cây không xóa được, chỉ chuyển sang ngừng sử dụng khi sửa. */}
        {mode === "edit" && (
          <Controller
            name="status"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="crop-status">
                  Trạng thái
                  <RequiredMark />
                </FieldLabel>
                <NativeSelect {...field} id="crop-status" className="w-full" required aria-invalid={fieldState.invalid}>
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
      </FieldGroup>

      {submitError !== null && <ErrorAlert error={submitError} />}
      {notice}

      <FormActionBar
        backTo={backTo}
        isDirty={form.formState.isDirty}
        submitLabel="Lưu cây trồng"
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
