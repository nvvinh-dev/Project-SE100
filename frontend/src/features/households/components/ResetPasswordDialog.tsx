import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm, type UseFormSetError } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Field, FieldDescription, FieldError, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { useResetMemberPassword } from "@/features/households/queries";
import { type ResetPasswordFormValues, resetPasswordFormSchema } from "@/features/households/schemas";
import { setFieldErrors } from "@/lib/form-errors";

interface ResetPasswordDialogProps {
  memberId: string;
  memberName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

/** Chủ hộ đặt mật khẩu mới cho thành viên quên mật khẩu (FR-HH-09). Thao tác ngắn nên dùng hộp thoại. */
export function ResetPasswordDialog({ memberId, memberName, open, onOpenChange }: ResetPasswordDialogProps) {
  const resetPassword = useResetMemberPassword(memberId);
  const pending = resetPassword.isPending;

  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Đặt lại mật khẩu</DialogTitle>
          <DialogDescription>
            {memberName} sẽ bị đăng xuất trên mọi thiết bị và đăng nhập lại bằng mật khẩu mới.
          </DialogDescription>
        </DialogHeader>
        {/* Dựng form mới mỗi lần mở để không giữ mật khẩu của lần trước. */}
        {open && (
          <ResetPasswordForm
            pending={pending}
            onCancel={() => onOpenChange(false)}
            onSubmit={(values, setError, setSubmitError) =>
              resetPassword.mutate(values, {
                onSuccess: () => {
                  onOpenChange(false);
                  showSuccess("Đã đặt lại mật khẩu. Hãy báo mật khẩu mới cho thành viên.");
                },
                onError: (error) => {
                  if (!setFieldErrors(error, setError, ["newPassword"])) {
                    setSubmitError(error);
                  }
                },
              })
            }
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

interface ResetPasswordFormProps {
  pending: boolean;
  onCancel: () => void;
  onSubmit: (
    values: ResetPasswordFormValues,
    setError: UseFormSetError<ResetPasswordFormValues>,
    setSubmitError: (error: unknown) => void,
  ) => void;
}

function ResetPasswordForm({ pending, onCancel, onSubmit }: ResetPasswordFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordFormSchema),
    defaultValues: { newPassword: "" },
  });

  return (
    <form
      noValidate
      onSubmit={form.handleSubmit((values) => {
        setSubmitError(null);
        onSubmit(values, form.setError, setSubmitError);
      })}
      className="flex flex-col gap-4"
    >
      <Controller
        name="newPassword"
        control={form.control}
        render={({ field, fieldState }) => (
          <Field data-invalid={fieldState.invalid}>
            <FieldLabel htmlFor="reset-new-password">Mật khẩu mới</FieldLabel>
            <Input
              {...field}
              id="reset-new-password"
              type="password"
              autoComplete="new-password"
              aria-invalid={fieldState.invalid}
            />
            <FieldDescription>Ít nhất 8 ký tự, có chữ và số.</FieldDescription>
            <FieldError errors={[fieldState.error]} />
          </Field>
        )}
      />
      {submitError !== null && <ErrorAlert error={submitError} />}
      <DialogFooter>
        <Button type="button" variant="outline" disabled={pending} onClick={onCancel}>
          Quay lại
        </Button>
        <Button type="submit" disabled={pending}>
          {pending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
          Đặt lại mật khẩu
        </Button>
      </DialogFooter>
    </form>
  );
}
