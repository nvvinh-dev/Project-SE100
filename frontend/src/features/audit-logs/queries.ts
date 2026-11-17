import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getAuditLogs } from "@/features/audit-logs/api";
import type { AuditLogQuery } from "@/features/audit-logs/types";

// Nhật ký chỉ đọc (FR-AUD-04): không có mutation. Thao tác ở module khác ghi thêm dòng mới; mở lại trang là thấy.
export const auditLogKeys = {
  all: ["audit-logs"] as const,
  list: (query: AuditLogQuery) => [...auditLogKeys.all, "list", query] as const,
};

export function useAuditLogs(query: AuditLogQuery) {
  return useQuery({
    queryKey: auditLogKeys.list(query),
    queryFn: () => getAuditLogs(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}
