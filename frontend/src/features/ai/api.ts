import type { AIQuotaResponse, AskAssistantRequest, AskAssistantResponse } from "@/features/ai/types";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";

export async function getAIQuota(): Promise<AIQuotaResponse> {
  const response = await apiClient.get<ResponseEnvelope<AIQuotaResponse>>("/ai/quota");
  return response.data.data;
}

export async function askAssistant(request: AskAssistantRequest): Promise<AskAssistantResponse> {
  const response = await apiClient.post<ResponseEnvelope<AskAssistantResponse>>("/ai/assistant", request);
  return response.data.data;
}
