import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getFinanceReport } from "@/features/reports/finance/api";
import type { FinanceReportQuery } from "@/features/reports/finance/types";

export const financeReportKeys = {
  all: ["reports", "finance"] as const,
  detail: (query: FinanceReportQuery) => [...financeReportKeys.all, query] as const,
};

export function useFinanceReport(query: FinanceReportQuery) {
  return useQuery({
    queryKey: financeReportKeys.detail(query),
    queryFn: () => getFinanceReport(query),
    // Giữ số liệu cũ trong lúc đổi bộ lọc để trang không nháy về khung xám.
    placeholderData: keepPreviousData,
  });
}
