import type {
  CorrectSaleRequest,
  CreateCustomerRequest,
  CreatePaymentRequest,
  CreateSaleRequest,
  CustomerListItem,
  CustomerQuery,
  CustomerResponse,
  PaymentResponse,
  SaleDebtsResponse,
  SaleListItem,
  SaleQuery,
  SaleResponse,
  UpdateCustomerRequest,
  VoidPaymentRequest,
  VoidSaleRequest,
} from "@/features/sales/types";
import type { HarvestRecordListItem } from "@/features/harvests/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";
import type { QualityGrade } from "@/lib/enums";

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


export async function correctSale(id: string, request: CorrectSaleRequest): Promise<SaleResponse> {
  const response = await apiClient.post<ResponseEnvelope<SaleResponse>>(`/sales/${id}/correct`, request);
  return response.data.data;
}

export async function voidSale(id: string, request: VoidSaleRequest): Promise<SaleResponse> {
  const response = await apiClient.post<ResponseEnvelope<SaleResponse>>(`/sales/${id}/void`, request);
  return response.data.data;
}

export async function getSaleDebts(): Promise<SaleDebtsResponse> {
  const response = await apiClient.get<ResponseEnvelope<SaleDebtsResponse>>("/sales/debts");
  return response.data.data;
}

export async function createPayment(request: CreatePaymentRequest): Promise<PaymentResponse> {
  const response = await apiClient.post<ResponseEnvelope<PaymentResponse>>("/payments", request);
  return response.data.data;
}

export async function voidPayment(id: string, request: VoidPaymentRequest): Promise<PaymentResponse> {
  const response = await apiClient.post<ResponseEnvelope<PaymentResponse>>(`/payments/${id}/void`, request);
  return response.data.data;
}

/**
 * Phiếu thu hoạch chọn được để liên kết (API của module thu hoạch, chế độ `forSaleLink`): cùng mùa vụ, cùng loại,
 * chưa hủy (BR-SAL-05). TV+TC xem được cả mùa vụ ngoài phân công ở chế độ này (BR-USR-13).
 */
export async function getHarvestsForSaleLink(seasonId: string, qualityGrade: QualityGrade): Promise<HarvestRecordListItem[]> {
  const response = await apiClient.get<ResponseEnvelope<HarvestRecordListItem[]>>("/harvest-records", {
    params: { seasonId, qualityGrade, forSaleLink: true, pageSize: 100 },
  });
  return response.data.data;
}
