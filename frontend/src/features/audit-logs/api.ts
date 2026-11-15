import type { AuditLogListItem, AuditLogQuery } from "@/features/audit-logs/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của nhật ký: `items` là `data`, thông tin trang ở `meta`. */
export interface AuditLogPage {
  items: AuditLogListItem[];
  meta: PageMeta;
}

export async function getAuditLogs(query: AuditLogQuery): Promise<AuditLogPage> {
  const response = await apiClient.get<ResponseEnvelope<AuditLogListItem[]>>("/audit-logs", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}
