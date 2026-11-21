import type { SupplyReportQuery, SupplyReportResponse } from "@/features/reports/supplies/types";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";

export async function getSupplyReport(query: SupplyReportQuery): Promise<SupplyReportResponse> {
  const response = await apiClient.get<ResponseEnvelope<SupplyReportResponse>>("/reports/supplies", { params: query });
  return response.data.data;
}
