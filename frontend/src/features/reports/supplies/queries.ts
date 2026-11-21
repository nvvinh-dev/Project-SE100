import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getSupplyReport } from "@/features/reports/supplies/api";
import type { SupplyReportQuery } from "@/features/reports/supplies/types";

export const supplyReportKeys = {
  all: ["reports", "supplies"] as const,
  detail: (query: SupplyReportQuery) => [...supplyReportKeys.all, query] as const,
};

export function useSupplyReport(query: SupplyReportQuery) {
  return useQuery({
    queryKey: supplyReportKeys.detail(query),
    queryFn: () => getSupplyReport(query),
    // Giữ số liệu cũ trong lúc đổi bộ lọc để trang không nháy về khung xám.
    placeholderData: keepPreviousData,
  });
}
