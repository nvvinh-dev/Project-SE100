import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useUpdateMember } from "@/features/households/queries";
import { type MemberInfoFormValues, memberInfoFormSchema } from "@/features/households/schemas";
import type { MemberResponse } from "@/features/households/types";
import { ApiError } from "@/lib/api-error";
import { setFieldErrors } from "@/lib/form-errors";
import { normalizePhone } from "@/lib/phone";

/** Sửa họ tên, số điện thoại của thành viên ngay trên trang chi tiết (FR-HH-04). Không thu hồi phiên của thành viên. */
export function MemberInfoForm({ member, onReload }: { member: MemberResponse; onReload: () => void }) {
  const updateMember = useUpdateMember(member.id);
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<MemberInfoFormValues>({
    resolver: zodResolver(memberInfoFormSchema),
    defaultValues: { fullName: member.fullName, phone: member.phone },
  });
  const isConflict = submitError instanceof ApiError && submitError.code === "CONCURRENCY_CONFLICT";

  function onSubmit(values: MemberInfoFormValues) {
    setSubmitError(null);
    updateMember.mutate(
      { fullName: values.fullName, phone: normalizePhone(values.phone) ?? values.phone, version: member.version },
      {
        onSuccess: () => showSuccess("Đã lưu thông tin thành viên."),
        // PHONE_ALREADY_USED hiện nguyên văn câu backend; giữ dữ liệu đã nhập.
        onError: (error) => {
          if (!setFieldErrors(error, form.setError, ["fullName", "phone"])) {
            setSubmitError(error);
          }
        },
      },
    );
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-5">
      <FieldGroup>
        <Controller
          name="fullName"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="member-full-name">
                Họ tên
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="member-full-name" autoComplete="off" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
        <Controller
          name="phone"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="member-phone">
                Số điện thoại
                <RequiredMark />
              </FieldLabel>
              <Input
                {...field}
                id="member-phone"
                type="tel"
                inputMode="tel"
                autoComplete="off"
                required
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {/* BR-GEN-11: thông tin thành viên vừa được sửa ở nơi khác; tải lại để thấy bản mới. */}
      {submitError !== null && (
        <ErrorAlert error={submitError} onRetry={isConflict ? onReload : undefined} retryLabel="Tải lại" />
      )}

      <Button type="submit" className="self-start" disabled={updateMember.isPending || !form.formState.isDirty}>
        {updateMember.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
        Lưu thông tin
      </Button>
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
