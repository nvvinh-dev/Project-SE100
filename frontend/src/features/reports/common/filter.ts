import type { ReportFilter } from "@/features/reports/common/types";

/** Đọc bộ lọc báo cáo từ URLSearchParams để đồng bộ đường dẫn khi tải lại hoặc quay lại trang. */
export function readReportFilter(params: URLSearchParams): ReportFilter {
  return {
    from: params.get("from") || undefined,
    to: params.get("to") || undefined,
    seasonId: params.get("seasonId") || undefined,
    plotId: params.get("plotId") || undefined,
    cropId: params.get("cropId") || undefined,
  };
}

/** Ghi các thay đổi bộ lọc vào URLSearchParams, bỏ tham số rỗng để đường dẫn gọn. */
export function applyFilterParams(
  params: URLSearchParams,
  changes: Record<string, string | undefined>,
): URLSearchParams {
  const next = new URLSearchParams(params);
  for (const [key, value] of Object.entries(changes)) {
    if (value) {
      next.set(key, value);
    } else {
      next.delete(key);
    }
  }
  return next;
}
