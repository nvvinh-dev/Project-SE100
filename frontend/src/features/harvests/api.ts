import type {
  CorrectHarvestRecordRequest,
  CreateHarvestRecordRequest,
  HarvestRecordListItem,
  HarvestRecordQuery,
  HarvestRecordResponse,
  VoidHarvestRecordRequest,
} from "@/features/harvests/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

export interface HarvestRecordPage {
  items: HarvestRecordListItem[];
  meta: PageMeta;
}

export async function getHarvests(query: HarvestRecordQuery): Promise<HarvestRecordPage> {
  const response = await apiClient.get<ResponseEnvelope<HarvestRecordListItem[]>>("/harvest-records", { params: query });
  const meta = response.data.meta ?? {
    page: 1,
    pageSize: response.data.data.length,
    totalCount: response.data.data.length,
  };
  return { items: response.data.data, meta };
}

export async function getHarvest(id: string): Promise<HarvestRecordResponse> {
  const response = await apiClient.get<ResponseEnvelope<HarvestRecordResponse>>(`/harvest-records/${id}`);
  return response.data.data;
}

export async function createHarvest(request: CreateHarvestRecordRequest): Promise<HarvestRecordResponse> {
  const response = await apiClient.post<ResponseEnvelope<HarvestRecordResponse>>("/harvest-records", request);
  return response.data.data;
}

export async function correctHarvest(id: string, request: CorrectHarvestRecordRequest): Promise<HarvestRecordResponse> {
  const response = await apiClient.post<ResponseEnvelope<HarvestRecordResponse>>(
    `/harvest-records/${id}/correct`,
    request,
  );
  return response.data.data;
}

export async function voidHarvest(id: string, request: VoidHarvestRecordRequest): Promise<HarvestRecordResponse> {
  const response = await apiClient.post<ResponseEnvelope<HarvestRecordResponse>>(`/harvest-records/${id}/void`, request);
  return response.data.data;
}
