import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Controller, type ControllerFieldState, type ControllerRenderProps, useForm } from "react-hook-form";
import { Link, useNavigate } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import {
  Field,
  FieldDescription,
  FieldError,
  FieldGroup,
  FieldLabel,
  FieldLegend,
  FieldSet,
} from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useRegister } from "@/features/auth/queries";
import { type RegisterFormValues, registerSchema } from "@/features/auth/schemas";
import { ApiError } from "@/lib/api-error";
import type { LoginLocationState } from "@/lib/auth/login-location-state";
import { setFieldErrors } from "@/lib/form-errors";
import { normalizePhone } from "@/lib/phone";

const FIELDS = ["fullName", "phone", "password", "householdName", "productionAddress", "contactInfo"] as const;

type FieldName = (typeof FIELDS)[number];

export function RegisterPage() {
  const navigate = useNavigate();
  const registerMutation = useRegister();
  const [submitError, setSubmitError] = useState<unknown>(null);

  const form = useForm<RegisterFormValues>({
    resolver: zodResolver(registerSchema),
    defaultValues: {
      fullName: "",
      phone: "",
      password: "",
      householdName: "",
      productionAddress: "",
      contactInfo: "",
    },
  });

  function onSubmit(values: RegisterFormValues) {
    setSubmitError(null);
    const phone = normalizePhone(values.phone) ?? values.phone;
    registerMutation.mutate(
      { ...values, phone, contactInfo: values.contactInfo === "" ? null : values.contactInfo },
      {
        // UC-AUTH-01: đăng ký chưa bắt đầu phiên; chuyển tới đăng nhập và điền sẵn số vừa đăng ký.
        onSuccess: () => {
          showSuccess("Đã đăng ký hộ mới. Đăng nhập để bắt đầu.");
          const state: LoginLocationState = { phone };
          void navigate("/login", { replace: true, state });
        },
        onError: (error) => {
          // BR-USR-02: số đã được dùng thì báo ngay dưới ô số điện thoại.
          if (error instanceof ApiError && error.code === "PHONE_ALREADY_USED") {
            form.setError("phone", { type: "server", message: error.message });
          } else if (!setFieldErrors(error, form.setError, FIELDS)) {
            setSubmitError(error);
          }
        },
      },
    );
  }

  function renderField(
    name: FieldName,
    label: string,
    required: boolean,
    renderControl: (
      field: ControllerRenderProps<RegisterFormValues, FieldName>,
      fieldState: ControllerFieldState,
      id: string,
    ) => ReactNode,
    description?: string,
  ) {
    const id = `register-${name}`;
    return (
      <Controller
        name={name}
        control={form.control}
        render={({ field, fieldState }) => (
          <Field data-invalid={fieldState.invalid}>
            <FieldLabel htmlFor={id}>
              {label}
              {required && (
                <span aria-hidden className="text-destructive">
                  *
                </span>
              )}
            </FieldLabel>
            {renderControl(field, fieldState, id)}
            {description && <FieldDescription>{description}</FieldDescription>}
            <FieldError errors={[fieldState.error]} />
          </Field>
        )}
      />
    );
  }

  return (
    <section className="flex flex-col gap-6">
      <div>
        <h1 className="text-xl font-semibold">Đăng ký hộ mới</h1>
        <p className="mt-1 text-sm text-muted-foreground">Tài khoản đăng ký là Chủ hộ của hộ mới.</p>
      </div>

      <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-6">
        <FieldSet>
          <FieldLegend>Chủ hộ</FieldLegend>
          <FieldGroup>
            {renderField("fullName", "Họ tên", true, (field, fieldState, id) => (
              <Input {...field} id={id} autoComplete="name" required aria-invalid={fieldState.invalid} />
            ))}
            {renderField("phone", "Số điện thoại", true, (field, fieldState, id) => (
              <Input
                {...field}
                id={id}
                type="tel"
                inputMode="tel"
                autoComplete="tel"
                required
                aria-invalid={fieldState.invalid}
              />
            ))}
            {renderField(
              "password",
              "Mật khẩu",
              true,
              (field, fieldState, id) => (
                <Input
                  {...field}
                  id={id}
                  type="password"
                  autoComplete="new-password"
                  required
                  aria-invalid={fieldState.invalid}
                />
              ),
              "Tối thiểu 8 ký tự, có cả chữ cái và chữ số.",
            )}
          </FieldGroup>
        </FieldSet>

        <FieldSet>
          <FieldLegend>Hộ</FieldLegend>
          <FieldGroup>
            {renderField("householdName", "Tên hộ", true, (field, fieldState, id) => (
              <Input {...field} id={id} required aria-invalid={fieldState.invalid} />
            ))}
            {renderField("productionAddress", "Địa chỉ khu sản xuất", true, (field, fieldState, id) => (
              <Textarea {...field} id={id} rows={2} required aria-invalid={fieldState.invalid} />
            ))}
            {renderField("contactInfo", "Thông tin liên hệ", false, (field, fieldState, id) => (
              <Input {...field} id={id} aria-invalid={fieldState.invalid} />
            ))}
          </FieldGroup>
        </FieldSet>

        {submitError !== null && <ErrorAlert error={submitError} />}

        <Button type="submit" disabled={registerMutation.isPending}>
          {registerMutation.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
          Đăng ký
        </Button>
      </form>

      <p className="text-center text-sm text-muted-foreground">
        Đã có tài khoản?{" "}
        <Link to="/login" className="font-medium text-primary underline-offset-4 hover:underline">
          Đăng nhập
        </Link>
      </p>
    </section>
  );
}
