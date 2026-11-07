import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useUpdateProfile } from "@/features/auth/queries";
import { type ProfileFormValues, profileSchema } from "@/features/auth/schemas";
import type { CurrentUserResponse } from "@/lib/auth/types";
import { setFieldErrors } from "@/lib/form-errors";
import { normalizePhone } from "@/lib/phone";

/** Sửa họ tên, số điện thoại của chính mình ngay trên trang Tài khoản (FR-AUTH-07). */
export function ProfileForm({ user }: { user: CurrentUserResponse }) {
  const updateProfile = useUpdateProfile();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<ProfileFormValues>({
    resolver: zodResolver(profileSchema),
    defaultValues: { fullName: user.fullName, phone: user.phone },
  });

  function onSubmit(values: ProfileFormValues) {
    setSubmitError(null);
    updateProfile.mutate(
      { fullName: values.fullName, phone: normalizePhone(values.phone) ?? values.phone },
      {
        onSuccess: (saved) => {
          // Đưa form về giá trị đã lưu (số điện thoại đã chuẩn hóa) để nút lưu không coi là còn thay đổi.
          form.reset({ fullName: saved.fullName, phone: saved.phone });
          showSuccess("Đã lưu thông tin cá nhân.");
        },
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
    <form noValidate onSubmit={form.handleSubmit(onSubmit, () => setSubmitError(null))} className="flex flex-col gap-5">
      <FieldGroup>
        <Controller
          name="fullName"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="account-full-name">
                Họ tên
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="account-full-name" autoComplete="name" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
        <Controller
          name="phone"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="account-phone">
                Số điện thoại
                <RequiredMark />
              </FieldLabel>
              <Input
                {...field}
                id="account-phone"
                type="tel"
                inputMode="tel"
                autoComplete="tel"
                required
                aria-invalid={fieldState.invalid}
              />
              <FieldDescription>Số điện thoại cũng là tên đăng nhập.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {submitError !== null && <ErrorAlert error={submitError} />}

      <Button type="submit" className="self-start" disabled={updateProfile.isPending || !form.formState.isDirty}>
        {updateProfile.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
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
