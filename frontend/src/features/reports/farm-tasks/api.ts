import type { FarmTaskReportQuery, FarmTaskReportResponse } from "@/features/reports/farm-tasks/types";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";

export async function getFarmTaskReport(query: FarmTaskReportQuery): Promise<FarmTaskReportResponse> {
  const response = await apiClient.get<ResponseEnvelope<FarmTaskReportResponse>>("/reports/farm-tasks", {
    params: query,
  });
  return response.data.data;
}
