import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getFarmTaskReport } from "@/features/reports/farm-tasks/api";
import type { FarmTaskReportQuery } from "@/features/reports/farm-tasks/types";

export const farmTaskReportKeys = {
  all: ["reports", "farm-tasks"] as const,
  detail: (query: FarmTaskReportQuery) => [...farmTaskReportKeys.all, query] as const,
};

export function useFarmTaskReport(query: FarmTaskReportQuery) {
  return useQuery({
    queryKey: farmTaskReportKeys.detail(query),
    queryFn: () => getFarmTaskReport(query),
    placeholderData: keepPreviousData,
  });
}
