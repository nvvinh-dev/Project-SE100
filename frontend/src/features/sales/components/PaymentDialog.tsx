import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { MoneyText } from "@/components/common/MoneyText";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Field, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { RequiredMark } from "@/features/sales/components/RequiredMark";
import { useCreatePayment } from "@/features/sales/queries";
import {
  paymentFormFields,
  type PaymentFormOutput,
  paymentFormSchema,
  type PaymentFormValues,
  toCreatePaymentRequest,
} from "@/features/sales/schemas";
import { todayInVietnam } from "@/features/supplies/schemas";
import { setFieldErrors } from "@/lib/form-errors";
import { useRequestId } from "@/lib/request-id";

interface PaymentDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  saleId: string;
  outstanding: number;
}

/** Hộp thoại "Ghi thanh toán" (FR-SAL-04): số tiền gợi ý sẵn số còn nợ, ngày mặc định hôm nay. */
export function PaymentDialog({ open, onOpenChange, saleId, outstanding }: PaymentDialogProps) {
  const createPayment = useCreatePayment();
  return (
    <Dialog open={open} onOpenChange={(next) => !createPayment.isPending && onOpenChange(next)}>
      <DialogContent showCloseButton={false}>
        {/* Nội dung dựng lại mỗi lần mở: requestId mới cho lần ghi mới, gửi lại trong cùng lần mở dùng lại giá trị cũ. */}
        {open && (
          <PaymentForm
            saleId={saleId}
            outstanding={outstanding}
            onDone={() => onOpenChange(false)}
            mutation={createPayment}
          />
        )}
      </DialogContent>
    </Dialog>
  );
}

function PaymentForm({
  saleId,
  outstanding,
  onDone,
  mutation,
}: {
  saleId: string;
  outstanding: number;
  onDone: () => void;
  mutation: ReturnType<typeof useCreatePayment>;
}) {
  // BR-GEN-10: sinh một lần khi mở hộp thoại, bấm lưu lại sau lỗi mạng không ghi trùng.
  const requestId = useRequestId();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<PaymentFormValues, unknown, PaymentFormOutput>({
    resolver: zodResolver(paymentFormSchema(outstanding)),
    defaultValues: { amount: String(outstanding).replace(".", ","), paymentDate: todayInVietnam(), note: "" },
  });

  async function submit(values: PaymentFormOutput) {
    setSubmitError(null);
    try {
      await mutation.mutateAsync(toCreatePaymentRequest(values, saleId, requestId));
      showSuccess("Đã ghi thanh toán.");
      onDone();
    } catch (error) {
      // PAYMENT_EXCEEDS_OUTSTANDING, ALREADY_VOIDED, SEASON_INVALID_STATE hiện nguyên văn, giữ dữ liệu đã nhập.
      if (!setFieldErrors(error, form.setError, paymentFormFields)) {
        setSubmitError(error);
      }
    }
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(submit)} className="grid gap-4">
      <DialogHeader>
        <DialogTitle>Ghi thanh toán</DialogTitle>
        <DialogDescription>
          Còn nợ <MoneyText amount={outstanding} />
        </DialogDescription>
      </DialogHeader>
      <FieldGroup>
        <Controller
          name="amount"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="payment-amount">
                Số tiền
                <RequiredMark />
              </FieldLabel>
              <div className="flex items-center gap-2">
                <Input {...field} id="payment-amount" inputMode="decimal" autoComplete="off" aria-invalid={fieldState.invalid} />
                <span className="shrink-0 text-sm text-muted-foreground">₫</span>
              </div>
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
        <Controller
          name="paymentDate"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="payment-date">
                Ngày nhận tiền
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="payment-date" type="date" aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
        <Controller
          name="note"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="payment-note">Ghi chú</FieldLabel>
              <Textarea {...field} id="payment-note" rows={2} aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>
      {submitError !== null && <ErrorAlert error={submitError} />}
      <DialogFooter>
        <Button type="button" variant="outline" onClick={onDone} disabled={mutation.isPending}>
          Quay lại
        </Button>
        <Button type="submit" disabled={mutation.isPending}>
          {mutation.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
          Ghi thanh toán
        </Button>
      </DialogFooter>
    </form>
  );
}
