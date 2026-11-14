import { zodResolver } from "@hookform/resolvers/zod";
import { Loader2Icon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { Field, FieldDescription, FieldError, FieldGroup, FieldLabel } from "@/components/ui/field";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { useHousehold, useUpdateHousehold } from "@/features/households/queries";
import { type HouseholdFormValues, householdFormSchema } from "@/features/households/schemas";
import type { HouseholdResponse } from "@/features/households/types";
import { ApiError } from "@/lib/api-error";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { setFieldErrors } from "@/lib/form-errors";

/** `/household`: thông tin hộ. Chủ hộ sửa ngay trên trang; Thành viên hộ chỉ xem (UC-HH-01). */
export function HouseholdPage() {
  const household = useHousehold();
  const { data: user } = useCurrentUser();
  const canEdit = user !== undefined && canAccess(user, ACCESS.owner);

  if (household.isPending) {
    return <LoadingState variant="form" />;
  }

  if (household.isError) {
    return <ErrorAlert error={household.error} onRetry={() => void household.refetch()} />;
  }

  const data = household.data;

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Thông tin hộ" />
      {canEdit ? (
        // Dựng lại form sau mỗi lần lưu hoặc tải lại, để giá trị và version luôn là bản mới nhất.
        <HouseholdForm key={data.version} household={data} onReload={() => void household.refetch()} />
      ) : (
        <section className="rounded-lg border p-4">
          <dl className="grid gap-x-6 gap-y-3 sm:grid-cols-2">
            <InfoRow label="Mã hộ">{data.code}</InfoRow>
            <InfoRow label="Tên hộ">{data.name}</InfoRow>
            <InfoRow label="Địa chỉ khu sản xuất" wide>
              {data.productionAddress}
            </InfoRow>
            <InfoRow label="Thông tin liên hệ" wide>
              {data.contactInfo ?? "—"}
            </InfoRow>
          </dl>
        </section>
      )}
    </div>
  );
}

function HouseholdForm({ household, onReload }: { household: HouseholdResponse; onReload: () => void }) {
  const updateHousehold = useUpdateHousehold();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<HouseholdFormValues>({
    resolver: zodResolver(householdFormSchema),
    defaultValues: {
      name: household.name,
      productionAddress: household.productionAddress,
      contactInfo: household.contactInfo ?? "",
    },
  });
  const isConflict = submitError instanceof ApiError && submitError.code === "CONCURRENCY_CONFLICT";

  function onSubmit(values: HouseholdFormValues) {
    setSubmitError(null);
    updateHousehold.mutate(
      {
        name: values.name,
        productionAddress: values.productionAddress,
        contactInfo: values.contactInfo === "" ? null : values.contactInfo,
        version: household.version,
      },
      {
        onSuccess: () => showSuccess("Đã lưu thông tin hộ."),
        // Lỗi theo trường gắn vào ô; lỗi khác hiện trên nút lưu, giữ nguyên dữ liệu đã nhập.
        onError: (error) => {
          if (!setFieldErrors(error, form.setError, ["name", "productionAddress", "contactInfo"])) {
            setSubmitError(error);
          }
        },
      },
    );
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-6">
      <FieldGroup>
        <Field>
          <FieldLabel htmlFor="household-code">Mã hộ</FieldLabel>
          <Input id="household-code" value={household.code} readOnly disabled />
          <FieldDescription>Mã hộ do hệ thống tự đặt, không sửa được.</FieldDescription>
        </Field>

        <Controller
          name="name"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="household-name">
                Tên hộ
                <RequiredMark />
              </FieldLabel>
              <Input {...field} id="household-name" required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="productionAddress"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="household-address">
                Địa chỉ khu sản xuất
                <RequiredMark />
              </FieldLabel>
              <Textarea {...field} id="household-address" rows={2} required aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />

        <Controller
          name="contactInfo"
          control={form.control}
          render={({ field, fieldState }) => (
            <Field data-invalid={fieldState.invalid}>
              <FieldLabel htmlFor="household-contact">Thông tin liên hệ</FieldLabel>
              <Input {...field} id="household-contact" aria-invalid={fieldState.invalid} />
              <FieldError errors={[fieldState.error]} />
            </Field>
          )}
        />
      </FieldGroup>

      {/* BR-GEN-11: người khác vừa sửa thông tin hộ; tải lại để thấy bản mới rồi sửa tiếp. */}
      {submitError !== null && (
        <ErrorAlert error={submitError} onRetry={isConflict ? onReload : undefined} retryLabel="Tải lại" />
      )}

      <Button type="submit" className="self-start" disabled={updateHousehold.isPending || !form.formState.isDirty}>
        {updateHousehold.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
        Lưu thông tin hộ
      </Button>
    </form>
  );
}

function InfoRow({ label, wide = false, children }: { label: string; wide?: boolean; children: ReactNode }) {
  return (
    <div className={wide ? "sm:col-span-2" : undefined}>
      <dt className="text-sm text-muted-foreground">{label}</dt>
      <dd className="break-words whitespace-pre-line">{children}</dd>
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
