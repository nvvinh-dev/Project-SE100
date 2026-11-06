import type {
  CreatePlotRequest,
  PlotListItem,
  PlotQuery,
  PlotResponse,
  UpdatePlotRequest,
} from "@/features/plots/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface PlotPage {
  items: PlotListItem[];
  meta: PageMeta;
}

export async function getPlots(query: PlotQuery): Promise<PlotPage> {
  const response = await apiClient.get<ResponseEnvelope<PlotListItem[]>>("/plots", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getPlot(id: string): Promise<PlotResponse> {
  const response = await apiClient.get<ResponseEnvelope<PlotResponse>>(`/plots/${id}`);
  return response.data.data;
}

export async function createPlot(request: CreatePlotRequest): Promise<PlotResponse> {
  const response = await apiClient.post<ResponseEnvelope<PlotResponse>>("/plots", request);
  return response.data.data;
}

export async function updatePlot(id: string, request: UpdatePlotRequest): Promise<PlotResponse> {
  const response = await apiClient.put<ResponseEnvelope<PlotResponse>>(`/plots/${id}`, request);
  return response.data.data;
}
