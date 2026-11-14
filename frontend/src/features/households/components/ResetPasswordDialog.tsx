import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
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
import { type ResetPasswordFormValues, resetPasswordFormSchema } from "@/features/households/schemas";
import { setFieldErrors } from "@/lib/form-errors";

interface ResetPasswordDialogProps {
  /** Họ tên chủ tài khoản được đặt lại mật khẩu. */
  accountName: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
  /** Gửi mật khẩu mới; lỗi ném ra được hiện trong hộp thoại, giữ nguyên ô đã nhập. */
  reset: (values: ResetPasswordFormValues) => Promise<unknown>;
  pending: boolean;
  /** Toast khi thành công, nhắc người đặt lại báo mật khẩu mới cho chủ tài khoản. */
  successMessage: string;
}

/**
 * Đặt mật khẩu mới cho người quên mật khẩu: Chủ hộ cho thành viên (FR-HH-09), Quản trị cho Chủ hộ (FR-ADM-03).
 * Thao tác ngắn nên dùng hộp thoại.
 */
export function ResetPasswordDialog({
  accountName,
  open,
  onOpenChange,
  reset,
  pending,
  successMessage,
}: ResetPasswordDialogProps) {
  return (
    <Dialog open={open} onOpenChange={(next) => !pending && onOpenChange(next)}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Đặt lại mật khẩu</DialogTitle>
          <DialogDescription>
            {accountName} sẽ bị đăng xuất trên mọi thiết bị và đăng nhập lại bằng mật khẩu mới.
          </DialogDescription>
        </DialogHeader>
        {/* Dựng form mới mỗi lần mở để không giữ mật khẩu của lần trước. */}
        {open && (
          <ResetPasswordForm
            pending={pending}
            onCancel={() => onOpenChange(false)}
            onSubmit={async (values) => {
              await reset(values);
              onOpenChange(false);
              showSuccess(successMessage);
            }}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

interface ResetPasswordFormProps {
  pending: boolean;
  onCancel: () => void;
  onSubmit: (values: ResetPasswordFormValues) => Promise<void>;
}

function ResetPasswordForm({ pending, onCancel, onSubmit }: ResetPasswordFormProps) {
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<ResetPasswordFormValues>({
    resolver: zodResolver(resetPasswordFormSchema),
    defaultValues: { newPassword: "" },
  });

  async function submit(values: ResetPasswordFormValues) {
    setSubmitError(null);
    try {
      await onSubmit(values);
    } catch (error) {
      if (!setFieldErrors(error, form.setError, ["newPassword"])) {
        setSubmitError(error);
      }
    }
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(submit)} className="flex flex-col gap-4">
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
