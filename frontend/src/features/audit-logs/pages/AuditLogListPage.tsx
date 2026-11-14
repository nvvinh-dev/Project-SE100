import { type ReactNode, useId } from "react";
import { useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { AuditLogChangesDialog } from "@/features/audit-logs/components/AuditLogChangesDialog";
import { auditActionLabel, auditActionLabels, auditActions, entityTypeLabel, isAuditAction } from "@/features/audit-logs/labels";
import { useAuditLogs } from "@/features/audit-logs/queries";
import type { AuditLogListItem, AuditLogQuery } from "@/features/audit-logs/types";
import { useMembers } from "@/features/households/queries";
import { formatDateTime } from "@/lib/format";

const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

/**
 * `/audit-logs`: nhật ký thao tác của hộ, chỉ Chủ hộ (FR-AUD-03). Lọc theo khoảng ngày, người thực hiện, loại hành động;
 * bộ lọc nằm trên đường dẫn. Chạm một dòng để xem các trường đã đổi. Không có thao tác sửa, xóa (FR-AUD-04).
 */
export function AuditLogListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const logs = useAuditLogs(query);
  // Người thực hiện chọn từ danh sách người trong hộ; một hộ ít người nên lấy một trang đủ lớn.
  const members = useMembers({ page: 1, pageSize: 100 });
  const openLogId = searchParams.get("log");
  const openLog = logs.data?.items.find((log) => log.id === openLogId) ?? null;
  const ids = { from: useId(), to: useId(), actor: useId(), action: useId() };

  // Đổi bộ lọc thì về trang 1; chỉ giữ tham số có giá trị để đường dẫn gọn.
  function updateParams(changes: Record<string, string | undefined>) {
    setSearchParams((params) => {
      const next = new URLSearchParams(params);
      for (const [key, value] of Object.entries(changes)) {
        if (value) {
          next.set(key, value);
        } else {
          next.delete(key);
        }
      }
      if (!("page" in changes) && !("log" in changes)) {
        next.delete("page");
      }
      return next;
    });
  }

  // Mở dòng nhật ký bằng tham số trên đường dẫn: quay lại thì đóng hộp thoại.
  function hrefOf(log: AuditLogListItem): string {
    const next = new URLSearchParams(searchParams);
    next.set("log", log.id);
    return `/audit-logs?${next.toString()}`;
  }

  const isFiltered = Boolean(query.from || query.to || query.actorId || query.action);

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Nhật ký thao tác" description="Ghi lại các thao tác quan trọng của hộ. Không ai sửa, xóa được." />

      <div className="grid gap-3 rounded-lg border p-3 sm:grid-cols-2 lg:grid-cols-4">
        <FilterField id={ids.from} label="Từ ngày">
          <Input
            id={ids.from}
            type="date"
            value={query.from ?? ""}
            max={query.to}
            onChange={(event) => updateParams({ from: event.target.value || undefined })}
          />
        </FilterField>
        <FilterField id={ids.to} label="Đến ngày">
          <Input
            id={ids.to}
            type="date"
            value={query.to ?? ""}
            min={query.from}
            onChange={(event) => updateParams({ to: event.target.value || undefined })}
          />
        </FilterField>
        <FilterField id={ids.actor} label="Người thực hiện">
          <NativeSelect
            id={ids.actor}
            className="w-full"
            value={query.actorId ?? ""}
            onChange={(event) => updateParams({ actorId: event.target.value || undefined })}
          >
            <NativeSelectOption value="">Tất cả</NativeSelectOption>
            {members.data?.items.map((member) => (
              <NativeSelectOption key={member.id} value={member.id}>
                {member.fullName}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </FilterField>
        <FilterField id={ids.action} label="Loại hành động">
          <NativeSelect
            id={ids.action}
            className="w-full"
            value={query.action ?? ""}
            onChange={(event) => updateParams({ action: event.target.value || undefined })}
          >
            <NativeSelectOption value="">Tất cả</NativeSelectOption>
            {auditActions.map((action) => (
              <NativeSelectOption key={action} value={action}>
                {auditActionLabels[action]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </FilterField>
        {isFiltered && (
          <Button
            type="button"
            variant="ghost"
            className="justify-self-start sm:col-span-2 lg:col-span-4"
            onClick={() => setSearchParams(new URLSearchParams())}
          >
            Bỏ lọc
          </Button>
        )}
      </div>

      {logs.isPending ? (
        <LoadingState variant="list" />
      ) : logs.isError ? (
        <ErrorAlert error={logs.error} onRetry={() => void logs.refetch()} />
      ) : logs.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có thao tác nào được ghi nhật ký." />
        )
      ) : (
        <>
          <DataList
            items={logs.data.items}
            getKey={(log) => log.id}
            getHref={hrefOf}
            title={(log) => auditActionLabel(log.action)}
            subtitle={logSummary}
            columns={[
              { header: "Thời điểm", cell: (log) => formatDateTime(log.occurredAt) },
              { header: "Người thực hiện", cell: (log) => log.actorName ?? "—" },
              { header: "Hành động", cell: (log) => auditActionLabel(log.action) },
              { header: "Đối tượng", cell: (log) => entityTypeLabel(log.entityType) },
              { header: "Lý do", cell: (log) => log.reason ?? "" },
            ]}
          />
          <Pagination meta={logs.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}

      <AuditLogChangesDialog log={openLog} onClose={() => updateParams({ log: undefined })} />
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: thời điểm · người thực hiện · đối tượng.
function logSummary(log: AuditLogListItem): string {
  return [formatDateTime(log.occurredAt), log.actorName, entityTypeLabel(log.entityType)].filter(Boolean).join(" · ");
}

function FilterField({ id, label, children }: { id: string; label: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-1.5">
      <label htmlFor={id} className="text-sm font-medium">
        {label}
      </label>
      {children}
    </div>
  );
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams): AuditLogQuery {
  const page = Number(params.get("page"));
  const date = (key: string) => {
    const value = params.get(key);
    return value && DATE_PATTERN.test(value) ? value : undefined;
  };
  const action = params.get("action");
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    from: date("from"),
    to: date("to"),
    actorId: params.get("actorId") ?? undefined,
    action: action && isAuditAction(action) ? action : undefined,
  };
}
