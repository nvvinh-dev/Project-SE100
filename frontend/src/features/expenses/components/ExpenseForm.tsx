import { zodResolver } from "@hookform/resolvers/zod";
import { type ReactNode, useCallback, useState } from "react";
import { Controller, useForm, type UseFormSetError, useWatch } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel, FieldLegend, FieldSet } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { Textarea } from "@/components/ui/textarea";
import {
  type ExpenseFormOutput,
  expenseFormSchema,
  type ExpenseFormValues,
  expenseScopes,
} from "@/features/expenses/schemas";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { expenseCategoryLabels } from "@/lib/enum-labels";
import { expenseCategories } from "@/lib/enums";
import { cn } from "@/lib/utils";

/** Kết quả gửi form: có lỗi thì form hiện lỗi đó và giữ nguyên dữ liệu đã nhập. */
export type ExpenseFormSubmit = (
  values: ExpenseFormOutput,
  setError: UseFormSetError<ExpenseFormValues>,
) => Promise<{ error: unknown } | void>;

interface ExpenseFormProps {
  mode: "create" | "correct";
  defaultValues: ExpenseFormValues;
  onSubmit: ExpenseFormSubmit;
  pending: boolean;
  backTo: string;
  submitLabel: string;
  /** Nội dung riêng của trang đặt trên nút lưu, ví dụ báo dữ liệu đã bị người khác thay đổi kèm nút tải lại. */
  notice?: ReactNode;
}

const scopeLabels: Record<(typeof expenseScopes)[number], string> = {
  season: "Chi phí của một mùa vụ",
  general: "Chi phí chung của hộ",
};

/**
 * Form ghi và điều chỉnh chi phí ghi tay (FR-EXP-01, FR-EXP-02). Chi phí vật tư không ghi ở đây mà tính từ xuất kho,
 * nên ô nhóm chỉ có bốn nhóm của `ExpenseCategory`.
 */
export function ExpenseForm({ mode, defaultValues, onSubmit, pending, backTo, submitLabel, notice }: ExpenseFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<ExpenseFormValues, unknown, ExpenseFormOutput>({
    resolver: zodResolver(expenseFormSchema),
    defaultValues,
  });
  const scope = useWatch({ control: form.control, name: "scope" });
  const category = useWatch({ control: form.control, name: "category" });

  // Ô chọn mùa vụ gọi lại hàm này trong effect khi nhận `?seasonId=`, nên giữ nguyên tham chiếu giữa các lần render.
  const selectSeason = useCallback(
    (seasonId: string) => form.setValue("seasonId", seasonId, { shouldDirty: true, shouldValidate: true }),
    [form],
  );

  async function submit(values: ExpenseFormOutput) {
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
          name="scope"
          control={form.control}
          render={({ field }) => (
            <FieldSet>
              <FieldLegend variant="label">Khoản chi thuộc về</FieldLegend>
              <div className="grid gap-2 sm:grid-cols-2">
                {expenseScopes.map((value) => (
                  <label
                    key={value}
                    className={cn(
                      "flex min-h-11 cursor-pointer items-center gap-3 rounded-lg border px-3 text-sm",
                      field.value === value && "border-primary bg-primary/5",
                    )}
                  >
                    <input
                      type="radio"
                      name={field.name}
                      value={value}
                      checked={field.value === value}
                      onChange={() => {
                        field.onChange(value);
                        // Đổi phạm vi thì lỗi "chưa chọn mùa vụ" không còn đúng.
                        form.clearErrors("seasonId");
                      }}
                      className="size-4 accent-primary"
                    />
                    {scopeLabels[value]}
                  </label>
                ))}
              </div>
            </FieldSet>
          )}
        />

        {scope === "season" && (
          <Controller
            name="seasonId"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="expense-season">
                  Mùa vụ
                  <RequiredMark />
                </FieldLabel>
                {/* BR-USR-13: TV+TC chọn được cả mùa vụ ngoài phân công; chỉ mùa vụ đang canh tác (BR-EXP-02). */}
                <SeasonSelect
                  id="expense-season"
                  value={field.value}
                  onChange={selectSeason}
                  operation="ManageExpenses"
                  invalid={fieldState.invalid}
                  allowInactiveFallback={mode === "correct"}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        <Controller
          name="category"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="expense-category">
                Nhóm chi phí
                <RequiredMark />
              </FieldLabel>
              <NativeSelect
                {...field}
                id="expense-category"
                className="w-full"
                required
                aria-invalid={fieldState.invalid}
              >
                <NativeSelectOption value="" disabled>
                  Chọn nhóm chi phí
                </NativeSelectOption>
                {expenseCategories.map((value) => (
                  <NativeSelectOption key={value} value={value}>
                    {expenseCategoryLabels[value]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
              {/* FR-EXP-01: chi phí vật tư tính từ giá trị xuất kho, ghi ở đây sẽ bị tính trùng. */}
              <FieldDescription>Giống, phân, thuốc, vật liệu tưới không ghi ở đây mà ghi qua xuất kho.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        {category === "Other" && (
          <Controller
            name="categoryOther"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="expense-category-other">Ghi chú nhóm</FieldLabel>
                <Input {...field} id="expense-category-other" autoComplete="off" aria-invalid={fieldState.invalid} />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        )}

        <Controller
          name="amount"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="expense-amount">
                Số tiền
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input
                  {...field}
                  id="expense-amount"
                  inputMode="decimal"
                  autoComplete="off"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <span className="shrink-0 text-sm text-muted-foreground">₫</span>
              </div>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="expenseDate"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="expense-date">
                Ngày phát sinh
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="expense-date" type="date" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="note"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="expense-note">Ghi chú</FieldLabel>
              <Textarea {...field} id="expense-note" rows={3} aria-invalid={fieldState.invalid} />
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

function RequiredMark() {
  return (
    <span aria-hidden className="text-destructive">
      *
    </span>
  );
}
