import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { getProductionReport } from "@/features/reports/production/api";
import type { ProductionReportQuery } from "@/features/reports/production/types";

export const productionReportKeys = {
  all: ["reports", "production"] as const,
  detail: (query: ProductionReportQuery) => [...productionReportKeys.all, query] as const,
};

export function useProductionReport(query: ProductionReportQuery) {
  return useQuery({
    queryKey: productionReportKeys.detail(query),
    queryFn: () => getProductionReport(query),
    placeholderData: keepPreviousData,
  });
}
