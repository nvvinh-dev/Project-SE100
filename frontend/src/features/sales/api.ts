import type {
  CreateCustomerRequest,
  CreateSaleRequest,
  CustomerListItem,
  CustomerQuery,
  CustomerResponse,
  ProduceStockResponse,
  SaleListItem,
  SaleQuery,
  SaleResponse,
  UpdateCustomerRequest,
} from "@/features/sales/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface Page<T> {
  items: T[];
  meta: PageMeta;
}

async function getPage<T>(path: string, params: object): Promise<Page<T>> {
  const response = await apiClient.get<ResponseEnvelope<T[]>>(path, { params });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export function getCustomers(query: CustomerQuery): Promise<Page<CustomerListItem>> {
  return getPage<CustomerListItem>("/customers", query);
}

export async function getCustomer(id: string): Promise<CustomerResponse> {
  const response = await apiClient.get<ResponseEnvelope<CustomerResponse>>(`/customers/${id}`);
  return response.data.data;
}

export async function createCustomer(request: CreateCustomerRequest): Promise<CustomerResponse> {
  const response = await apiClient.post<ResponseEnvelope<CustomerResponse>>("/customers", request);
  return response.data.data;
}

export async function updateCustomer(id: string, request: UpdateCustomerRequest): Promise<CustomerResponse> {
  const response = await apiClient.put<ResponseEnvelope<CustomerResponse>>(`/customers/${id}`, request);
  return response.data.data;
}

export function getSales(query: SaleQuery): Promise<Page<SaleListItem>> {
  return getPage<SaleListItem>("/sales", query);
}

export async function getSale(id: string): Promise<SaleResponse> {
  const response = await apiClient.get<ResponseEnvelope<SaleResponse>>(`/sales/${id}`);
  return response.data.data;
}

export async function createSale(request: CreateSaleRequest): Promise<SaleResponse> {
  const response = await apiClient.post<ResponseEnvelope<SaleResponse>>("/sales", request);
  return response.data.data;
}

/** Tồn nông sản của mùa vụ (API của module thu hoạch); trả mọi loại chất lượng, trang tự chọn loại cần. */
export async function getProduceStock(seasonId: string): Promise<ProduceStockResponse[]> {
  const response = await apiClient.get<ResponseEnvelope<ProduceStockResponse[]>>("/produce-stock", { params: { seasonId } });
  return response.data.data;
}
