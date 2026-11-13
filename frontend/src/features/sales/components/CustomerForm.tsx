import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useState } from "react";
import { Controller, useForm, type UseFormSetError } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import { RequiredMark } from "@/features/sales/components/RequiredMark";
import { type CustomerFormOutput, customerFormSchema, type CustomerFormValues } from "@/features/sales/schemas";
import { recordStatusLabels } from "@/lib/enum-labels";
import { recordStatuses } from "@/lib/enums";

/** Kết quả gửi form: có lỗi thì form hiện lỗi đó và giữ nguyên dữ liệu đã nhập (NFR-09). */
export type CustomerFormSubmit = (
  values: CustomerFormOutput,
  setError: UseFormSetError<CustomerFormValues>,
) => Promise<{ error: unknown } | void>;

interface CustomerFormProps {
  defaultValues: CustomerFormValues;
  onSubmit: CustomerFormSubmit;
  pending: boolean;
  backTo: string;
  notice?: ReactNode;
}

/** Form tạo, sửa khách hàng: tên, số điện thoại, ghi chú, trạng thái. */
export function CustomerForm({ defaultValues, onSubmit, pending, backTo, notice }: CustomerFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<CustomerFormValues, unknown, CustomerFormOutput>({
    resolver: zodResolver(customerFormSchema),
    defaultValues,
  });

  async function submit(values: CustomerFormOutput) {
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
              <FieldLabel htmlFor="customer-name">
                Tên khách hàng
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="customer-name" autoComplete="off" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="phone"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="customer-phone">Số điện thoại</FieldLabel>
              <Input
                {...field}
                id="customer-phone"
                type="tel"
                inputMode="tel"
                autoComplete="off"
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="status"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="customer-status">
                Tình trạng sử dụng
                <RequiredMark />
              </FieldLabel>
              <NativeSelect {...field} id="customer-status" className="w-full" required aria-invalid={fieldState.invalid}>
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

        <Controller
          name="note"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="customer-note">Ghi chú</FieldLabel>
              <Textarea {...field} id="customer-note" rows={3} aria-invalid={fieldState.invalid} />
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
        submitLabel="Lưu khách hàng"
        pending={pending}
      />
    </form>
  );
}
