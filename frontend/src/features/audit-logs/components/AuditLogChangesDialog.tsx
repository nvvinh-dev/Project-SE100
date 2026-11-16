import { ArrowRightIcon } from "lucide-react";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { auditActionLabel, entityTypeLabel, fieldLabel, formatAuditValue } from "@/features/audit-logs/labels";
import type { AuditLogListItem } from "@/features/audit-logs/types";
import { formatDateTime } from "@/lib/format";

/** Chi tiết một dòng nhật ký: các trường đã đổi dạng "giá trị cũ → giá trị mới" (FR-AUD-02). Chỉ xem, không sửa được. */
export function AuditLogChangesDialog({ log, onClose }: { log: AuditLogListItem | null; onClose: () => void }) {
  const fields = log ? changedFields(log) : [];

  return (
    <Dialog open={log !== null} onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        {log && (
          <>
            <DialogHeader>
              <DialogTitle>{auditActionLabel(log.action)}</DialogTitle>
              <DialogDescription>
                {formatDateTime(log.occurredAt)} · {log.actorName ?? "Không rõ người thực hiện"} ·{" "}
                {entityTypeLabel(log.entityType)}
              </DialogDescription>
            </DialogHeader>

            {log.reason && (
              <p className="text-sm">
                <span className="text-muted-foreground">Lý do: </span>
                {log.reason}
              </p>
            )}

            {fields.length === 0 ? (
              <p className="text-sm text-muted-foreground">Thao tác này không đổi trường dữ liệu nào.</p>
            ) : (
              <dl className="flex flex-col divide-y rounded-lg border">
                {fields.map((field) => (
                  <div key={field} className="flex flex-col gap-1 p-3">
                    <dt className="text-sm text-muted-foreground">{fieldLabel(field)}</dt>
                    <dd className="flex flex-wrap items-center gap-2 break-words">
                      <span>{formatAuditValue(field, log.oldValues?.[field])}</span>
                      <ArrowRightIcon className="size-4 shrink-0 text-muted-foreground" aria-label="thành" />
                      <span className="font-medium">{formatAuditValue(field, log.newValues?.[field])}</span>
                    </dd>
                  </div>
                ))}
              </dl>
            )}

            <DialogFooter>
              <Button type="button" variant="outline" onClick={onClose}>
                Quay lại
              </Button>
            </DialogFooter>
          </>
        )}
      </DialogContent>
    </Dialog>
  );
}

// Trường có trong giá trị cũ hoặc mới, giữ thứ tự xuất hiện.
function changedFields(log: AuditLogListItem): string[] {
  return [...new Set([...Object.keys(log.oldValues ?? {}), ...Object.keys(log.newValues ?? {})])];
}
