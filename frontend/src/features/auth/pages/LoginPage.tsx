import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { Link, useLocation } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { Button } from "@/components/ui/button";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useLogin } from "@/features/auth/queries";
import { type LoginFormValues, loginSchema } from "@/features/auth/schemas";
import { useAuth } from "@/lib/auth/auth-context";
import { readLoginLocationState } from "@/lib/auth/login-location-state";
import { setFieldErrors } from "@/lib/form-errors";
import { normalizePhone } from "@/lib/phone";

export function LoginPage() {
  const location = useLocation();
  const { signIn } = useAuth();
  const loginMutation = useLogin();
  const [submitError, setSubmitError] = useState<unknown>(null);

  const form = useForm<LoginFormValues>({
    resolver: zodResolver(loginSchema),
    // Vừa đăng ký xong thì điền sẵn số điện thoại vừa dùng.
    defaultValues: { phone: readLoginLocationState(location.state).phone ?? "", password: "" },
  });

  function onSubmit(values: LoginFormValues) {
    setSubmitError(null);
    loginMutation.mutate(
      { phone: normalizePhone(values.phone) ?? values.phone, password: values.password },
      {
        // Chuyển trang do RedirectIfAuthenticated làm khi đã nạp được người dùng hiện tại.
        onSuccess: (response) => signIn(response.accessToken),
        // INVALID_CREDENTIALS, TOO_MANY_ATTEMPTS, ACCOUNT_LOCKED hiện nguyên văn câu backend; giữ dữ liệu đã nhập.
        onError: (error) => {
          if (!setFieldErrors(error, form.setError, ["phone", "password"])) {
            setSubmitError(error);
          }
        },
      },
    );
  }

  return (
    <section className="flex flex-col gap-6">
      <h1 className="text-xl font-semibold">Đăng nhập</h1>

      <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-5">
        <FieldGroup>
          <Controller
            name="phone"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="login-phone">
                  Số điện thoại
                  <span aria-hidden className="text-destructive">*</span>
                </FieldLabel>
                <Input
                  {...field}
                  id="login-phone"
                  type="tel"
                  inputMode="tel"
                  autoComplete="username"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
          <Controller
            name="password"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="login-password">
                  Mật khẩu
                  <span aria-hidden className="text-destructive">*</span>
                </FieldLabel>
                <Input
                  {...field}
                  id="login-password"
                  type="password"
                  autoComplete="current-password"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {submitError !== null && <ErrorAlert error={submitError} />}

        <Button type="submit" disabled={loginMutation.isPending}>
          {loginMutation.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
          Đăng nhập
        </Button>
      </form>

      <p className="text-center text-sm text-muted-foreground">
        Chưa có tài khoản?{" "}
        <Link
          to="/register"
          className="inline-flex min-h-11 items-center font-medium text-primary underline-offset-4 hover:underline"
        >
          Đăng ký hộ mới
        </Link>
      </p>
    </section>
  );
}
