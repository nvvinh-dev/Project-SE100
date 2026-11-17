import { zodResolver } from "@hookform/resolvers/zod";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useNavigate } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { FormActionBar } from "@/components/common/FormActionBar";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useCreateMember } from "@/features/households/queries";
import {
  type CreateMemberFormOutput,
  createMemberFormSchema,
  type CreateMemberFormValues,
} from "@/features/households/schemas";
import { memberRoles } from "@/features/households/types";
import { userRoleLabels } from "@/lib/enum-labels";
import { setFieldErrors } from "@/lib/form-errors";
import { normalizePhone } from "@/lib/phone";

const emptyValues: CreateMemberFormValues = { fullName: "", phone: "", role: "", initialPassword: "" };

/** `/members/new`: Chủ hộ tạo tài khoản thành viên (FR-HH-02). */
export function MemberCreatePage() {
  const navigate = useNavigate();
  const createMember = useCreateMember();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<CreateMemberFormValues, unknown, CreateMemberFormOutput>({
    resolver: zodResolver(createMemberFormSchema),
    defaultValues: emptyValues,
  });

  async function onSubmit(values: CreateMemberFormOutput) {
    setSubmitError(null);
    try {
      const member = await createMember.mutateAsync({
        ...values,
        phone: normalizePhone(values.phone) ?? values.phone,
      });
      showSuccess(`Đã thêm tài khoản ${member.fullName}.`);
      // Thay trang form trong lịch sử: bấm quay lại từ trang chi tiết về danh sách, không mở lại form trống.
      void navigate(`/members/${member.id}`, { replace: true });
    } catch (error) {
      // PHONE_ALREADY_USED hiện nguyên văn câu backend; giữ nguyên dữ liệu đã nhập.
      if (!setFieldErrors(error, form.setError, ["fullName", "phone", "role", "initialPassword"])) {
        setSubmitError(error);
      }
    }
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thêm thành viên" backTo="/members" />
      <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-6">
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
                <FieldDescription>Số điện thoại cũng là tên đăng nhập của thành viên.</FieldDescription>
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="role"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="member-role">
                  Vai trò
                  <RequiredMark />
                </FieldLabel>
                <NativeSelect {...field} id="member-role" className="w-full" required aria-invalid={fieldState.invalid}>
                  <NativeSelectOption value="" disabled>
                    Chọn vai trò
                  </NativeSelectOption>
                  {memberRoles.map((value) => (
                    <NativeSelectOption key={value} value={value}>
                      {userRoleLabels[value]}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />

          <Controller
            name="initialPassword"
            control={form.control}
            render={({ field, fieldState }) => (
              <Field data-invalid={fieldState.invalid}>
                <FieldLabel htmlFor="member-password">
                  Mật khẩu ban đầu
                  <RequiredMark />
                </FieldLabel>
                <Input
                  {...field}
                  id="member-password"
                  type="password"
                  autoComplete="new-password"
                  required
                  aria-invalid={fieldState.invalid}
                />
                <FieldDescription>
                  Ít nhất 8 ký tự, có chữ và số. Hệ thống không gửi email, tin nhắn: Chủ hộ tự báo mật khẩu cho thành viên.
                </FieldDescription>
                <FieldError errors={[fieldState.error]} />
              </Field>
            )}
          />
        </FieldGroup>

        {submitError !== null && <ErrorAlert error={submitError} />}

        <FormActionBar
          backTo="/members"
          isDirty={form.formState.isDirty}
          submitLabel="Lưu thành viên"
          pending={createMember.isPending}
        />
      </form>
    </div>
  );
}

function RequiredMark() {
  return (
    <span aria-hidden className="text-destructive">
      *
    </span>
  );
}
