import type { PageQuery } from "@/lib/api-client";

// Trùng tên DTO backend (Features/AuditLogs/AuditLogDtos.cs).

/** Giá trị cũ hoặc mới của một dòng nhật ký: chỉ các trường bị đổi, tên trường như trong API. */
export type AuditValues = Record<string, unknown>;

export interface AuditLogListItem {
  id: string;
  /** Thời điểm UTC; hiện theo giờ Việt Nam bằng `formatDateTime`. */
  occurredAt: string;
  actorId: string | null;
  /** Họ tên người thực hiện, có thể là Quản trị hệ thống. */
  actorName: string | null;
  /** Dạng `<Entity>.<Thao tác>`, ví dụ `Season.Close`. */
  action: string;
  entityType: string;
  entityId: string | null;
  reason: string | null;
  oldValues: AuditValues | null;
  newValues: AuditValues | null;
}

/** Luôn mới nhất trước. `from`, `to` là ngày `yyyy-MM-dd` theo giờ Việt Nam, tính cả hai đầu. */
export interface AuditLogQuery extends Omit<PageQuery, "sort" | "search"> {
  from?: string;
  to?: string;
  actorId?: string;
  action?: string;
}
