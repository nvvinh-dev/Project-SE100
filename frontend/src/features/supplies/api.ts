import type {
  CreateStockTransactionRequest,
  CreateSupplyRequest,
  StockTransactionListItem,
  StockTransactionQuery,
  StockTransactionResponse,
  SupplyListItem,
  SupplyQuery,
  SupplyResponse,
  UpdateSupplyRequest,
  VoidStockTransactionRequest,
} from "@/features/supplies/types";
import { apiClient, type PageMeta, type ResponseEnvelope, type ResponseWarning } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface SupplyPage {
  items: SupplyListItem[];
  meta: PageMeta;
}

export async function getSupplies(query: SupplyQuery): Promise<SupplyPage> {
  const response = await apiClient.get<ResponseEnvelope<SupplyListItem[]>>("/supplies", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getSupply(id: string): Promise<SupplyResponse> {
  const response = await apiClient.get<ResponseEnvelope<SupplyResponse>>(`/supplies/${id}`);
  return response.data.data;
}

export async function createSupply(request: CreateSupplyRequest): Promise<SupplyResponse> {
  const response = await apiClient.post<ResponseEnvelope<SupplyResponse>>("/supplies", request);
  return response.data.data;
}

export async function updateSupply(id: string, request: UpdateSupplyRequest): Promise<SupplyResponse> {
  const response = await apiClient.put<ResponseEnvelope<SupplyResponse>>(`/supplies/${id}`, request);
  return response.data.data;
}

/** Kết quả tạo giao dịch kho: `warnings` hiện bằng toast (`LOW_STOCK`, `NEGATIVE_STOCK`). */
export interface StockTransactionResult {
  transaction: StockTransactionResponse;
  warnings: ResponseWarning[];
}

export async function createStockTransaction(request: CreateStockTransactionRequest): Promise<StockTransactionResult> {
  const response = await apiClient.post<ResponseEnvelope<StockTransactionResponse>>("/stock-transactions", request);
  return { transaction: response.data.data, warnings: response.data.warnings };
}

/** Một trang của lịch sử giao dịch kho. */
export interface StockTransactionPage {
  items: StockTransactionListItem[];
  meta: PageMeta;
}

export async function getStockTransactions(query: StockTransactionQuery): Promise<StockTransactionPage> {
  const response = await apiClient.get<ResponseEnvelope<StockTransactionListItem[]>>("/stock-transactions", {
    params: query,
  });
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function voidStockTransaction(
  id: string,
  request: VoidStockTransactionRequest,
): Promise<StockTransactionResult> {
  const response = await apiClient.post<ResponseEnvelope<StockTransactionResponse>>(`/stock-transactions/${id}/void`, request);
  return { transaction: response.data.data, warnings: response.data.warnings };
}
