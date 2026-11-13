import { Loader2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import { SwitchRow } from "@/features/households/components/SwitchRow";
import { useChangeHouseholdSettings, useHouseholdSettings } from "@/features/households/queries";
import type { SettingsFormValues } from "@/features/households/schemas";
import type { HouseholdSettingsResponse } from "@/features/households/types";
import { ApiError } from "@/lib/api-error";

/** `/household/settings`: hai công tắc cài đặt hộ, chỉ Chủ hộ (UC-HH-02). */
export function HouseholdSettingsPage() {
  const settings = useHouseholdSettings();

  if (settings.isPending) {
    return <LoadingState variant="form" />;
  }

  if (settings.isError) {
    return <ErrorAlert error={settings.error} onRetry={() => void settings.refetch()} />;
  }

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title="Cài đặt hộ" />
      {/* Dựng lại form sau mỗi lần lưu hoặc tải lại, để giá trị và version luôn là bản mới nhất. */}
      <SettingsForm key={settings.data.version} settings={settings.data} onReload={() => void settings.refetch()} />
    </div>
  );
}

function SettingsForm({ settings, onReload }: { settings: HouseholdSettingsResponse; onReload: () => void }) {
  const changeSettings = useChangeHouseholdSettings();
  const [submitError, setSubmitError] = useState<unknown>(null);
  const form = useForm<SettingsFormValues>({
    defaultValues: {
      allowNegativeStock: settings.allowNegativeStock,
      allowOverlappingSeasons: settings.allowOverlappingSeasons,
    },
  });
  const isConflict = submitError instanceof ApiError && submitError.code === "CONCURRENCY_CONFLICT";

  function onSubmit(values: SettingsFormValues) {
    setSubmitError(null);
    changeSettings.mutate(
      { ...values, version: settings.version },
      {
        onSuccess: () => showSuccess("Đã lưu cài đặt hộ."),
        onError: setSubmitError,
      },
    );
  }

  return (
    <form noValidate onSubmit={form.handleSubmit(onSubmit)} className="flex flex-col gap-6">
      <div className="divide-y rounded-lg border px-4">
        <Controller
          name="allowNegativeStock"
          control={form.control}
          render={({ field }) => (
            <SwitchRow
              id="settings-negative-stock"
              label="Cho phép âm kho"
              description="Cho phép xuất kho khi tồn không đủ; tồn sẽ âm và có cảnh báo."
              checked={field.value}
              onCheckedChange={field.onChange}
            />
          )}
        />
        <Controller
          name="allowOverlappingSeasons"
          control={form.control}
          render={({ field }) => (
            <SwitchRow
              id="settings-overlapping-seasons"
              label="Cho phép mùa vụ chồng lấn"
              description="Cho phép tạo mùa vụ mới trên thửa đang có mùa vụ khác trùng thời gian."
              checked={field.value}
              onCheckedChange={field.onChange}
            />
          )}
        />
      </div>

      {/* BR-GEN-11: thông tin hộ hoặc cài đặt vừa được sửa ở nơi khác; tải lại để thấy bản mới. */}
      {submitError !== null && (
        <ErrorAlert error={submitError} onRetry={isConflict ? onReload : undefined} retryLabel="Tải lại" />
      )}

      <Button type="submit" className="self-start" disabled={changeSettings.isPending || !form.formState.isDirty}>
        {changeSettings.isPending && <Loader2Icon data-icon="inline-start" className="animate-spin" />}
        Lưu cài đặt
      </Button>
    </form>
  );
}
