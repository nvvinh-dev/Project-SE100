import type { ProductionReportQuery, ProductionReportResponse } from "@/features/reports/production/types";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";

export async function getProductionReport(query: ProductionReportQuery): Promise<ProductionReportResponse> {
  const response = await apiClient.get<ResponseEnvelope<ProductionReportResponse>>("/reports/production", {
    params: query,
  });
  return response.data.data;
}
