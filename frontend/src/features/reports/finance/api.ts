import type { FinanceReportQuery, FinanceReportResponse } from "@/features/reports/finance/types";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";

export async function getFinanceReport(query: FinanceReportQuery): Promise<FinanceReportResponse> {
  const response = await apiClient.get<ResponseEnvelope<FinanceReportResponse>>("/reports/finance", { params: query });
  return response.data.data;
}
