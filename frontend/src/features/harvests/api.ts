import type {
  CorrectHarvestRecordRequest,
  CreateHarvestRecordRequest,
  CreateProduceStockAdjustmentRequest,
  HarvestRecordListItem,
  HarvestRecordQuery,
  HarvestRecordResponse,
  ProduceStockAdjustmentQuery,
  ProduceStockAdjustmentResponse,
  ProduceStockOverviewItem,
  ProduceStockOverviewQuery,
  VoidHarvestRecordRequest,
  VoidProduceStockAdjustmentRequest,
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

export interface ProduceStockAdjustmentPage {
  items: ProduceStockAdjustmentResponse[];
  meta: PageMeta;
}

export async function getProduceStockOverview(
  query: ProduceStockOverviewQuery,
): Promise<ProduceStockOverviewItem[]> {
  const response = await apiClient.get<ResponseEnvelope<ProduceStockOverviewItem[]>>("/produce-stock", {
    params: query,
  });
  return response.data.data;
}

export async function getProduceStockAdjustments(
  query: ProduceStockAdjustmentQuery,
): Promise<ProduceStockAdjustmentPage> {
  const response = await apiClient.get<ResponseEnvelope<ProduceStockAdjustmentResponse[]>>(
    "/produce-stock-adjustments",
    { params: query },
  );
  const meta = response.data.meta ?? {
    page: 1,
    pageSize: response.data.data.length,
    totalCount: response.data.data.length,
  };
  return { items: response.data.data, meta };
}

export async function createProduceStockAdjustment(
  request: CreateProduceStockAdjustmentRequest,
): Promise<ProduceStockAdjustmentResponse> {
  const response = await apiClient.post<ResponseEnvelope<ProduceStockAdjustmentResponse>>(
    "/produce-stock-adjustments",
    request,
  );
  return response.data.data;
}

export async function voidProduceStockAdjustment(
  id: string,
  request: VoidProduceStockAdjustmentRequest,
): Promise<ProduceStockAdjustmentResponse> {
  const response = await apiClient.post<ResponseEnvelope<ProduceStockAdjustmentResponse>>(
    `/produce-stock-adjustments/${id}/void`,
    request,
  );
  return response.data.data;
}
