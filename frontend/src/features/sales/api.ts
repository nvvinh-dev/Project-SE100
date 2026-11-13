import type {
  CreateCustomerRequest,
  CustomerListItem,
  CustomerQuery,
  CustomerResponse,
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
