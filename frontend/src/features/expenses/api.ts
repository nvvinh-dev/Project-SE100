import type {
  CorrectExpenseRequest,
  CreateExpenseRequest,
  ExpenseListItem,
  ExpenseQuery,
  ExpenseResponse,
  VoidExpenseRequest,
} from "@/features/expenses/types";
import { apiClient, type PageMeta, type ResponseEnvelope } from "@/lib/api-client";

/** Một trang của danh sách: `items` là `data`, thông tin trang ở `meta`. */
export interface ExpensePage {
  items: ExpenseListItem[];
  meta: PageMeta;
}

export async function getExpenses(query: ExpenseQuery): Promise<ExpensePage> {
  const response = await apiClient.get<ResponseEnvelope<ExpenseListItem[]>>("/expenses", { params: query });
  // API danh sách luôn trả meta; mặc định chỉ để kiểu không cho phép thiếu.
  const meta = response.data.meta ?? { page: 1, pageSize: response.data.data.length, totalCount: response.data.data.length };
  return { items: response.data.data, meta };
}

export async function getExpense(id: string): Promise<ExpenseResponse> {
  const response = await apiClient.get<ResponseEnvelope<ExpenseResponse>>(`/expenses/${id}`);
  return response.data.data;
}

export async function createExpense(request: CreateExpenseRequest): Promise<ExpenseResponse> {
  const response = await apiClient.post<ResponseEnvelope<ExpenseResponse>>("/expenses", request);
  return response.data.data;
}

export async function correctExpense(id: string, request: CorrectExpenseRequest): Promise<ExpenseResponse> {
  const response = await apiClient.post<ResponseEnvelope<ExpenseResponse>>(`/expenses/${id}/correct`, request);
  return response.data.data;
}

export async function voidExpense(id: string, request: VoidExpenseRequest): Promise<ExpenseResponse> {
  const response = await apiClient.post<ResponseEnvelope<ExpenseResponse>>(`/expenses/${id}/void`, request);
  return response.data.data;
}
