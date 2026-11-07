import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useChangePassword } from "@/features/auth/queries";
import { type ChangePasswordFormValues, changePasswordSchema } from "@/features/auth/schemas";
import { setFieldErrors } from "@/lib/form-errors";

/** Đổi mật khẩu của chính mình (FR-AUTH-05). Thành công thì mọi phiên kết thúc, người dùng đăng nhập lại. */
export function ChangePasswordForm() {
  const changePassword = useChangePassword();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: "", newPassword: "" },
  });

  function onSubmit(values: ChangePasswordFormValues) {
    setSubmitError(null);
    // Thành công: useChangePassword báo và kết thúc phiên.
    changePassword.mutate(values, {
      // INVALID_CREDENTIALS (sai mật khẩu hiện tại) hiện nguyên văn câu backend; giữ dữ liệu đã nhập.
      onError: (error) => {
        if (!setFieldErrors(error, form.setError, ["currentPassword", "newPassword"])) {
          setSubmitError(error);
        }
      },
    });
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit, () => setSubmitError(null))} className="flex flex-col gap-5">
      <FieldGroup>
        <Controller
          name="currentPassword"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="account-current-password">
                Mật khẩu hiện tại
                <RequiredMark />
              </FieldLabel>
              <Input
                {...field}
                id="account-current-password"
                type="password"
                autoComplete="current-password"
                required
                aria-invalid={fieldState.invalid}
              />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
        <Controller
          name="newPassword"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="account-new-password">
                Mật khẩu mới
                <RequiredMark />
              </FieldLabel>
              <Input
                {...field}
                id="account-new-password"
                type="password"
                autoComplete="new-password"
                required
                aria-invalid={fieldState.invalid}
              />
              <FieldDescription>Ít nhất 8 ký tự, có chữ cái và chữ số.</FieldDescription>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {submitError !== null && <ErrorAlert error={submitError} />}

      <Button type="submit" className="self-start" disabled={changePassword.isPending}>
        {changePassword.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
        Đổi mật khẩu
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
