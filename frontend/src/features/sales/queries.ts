import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createCustomer,
  createSale,
  getCustomer,
  getCustomers,
  getProduceStock,
  getSale,
  getSales,
  updateCustomer,
} from "@/features/sales/api";
import type { CustomerQuery, SaleQuery, UpdateCustomerRequest } from "@/features/sales/types";
import { seasonKeys } from "@/features/seasons/queries";

export const customerKeys = {
  all: ["customers"] as const,
  list: (query: CustomerQuery) => [...customerKeys.all, "list", query] as const,
  detail: (id: string) => [...customerKeys.all, "detail", id] as const,
};

export const saleKeys = {
  all: ["sales"] as const,
  list: (query: SaleQuery) => [...saleKeys.all, "list", query] as const,
  detail: (id: string) => [...saleKeys.all, "detail", id] as const,
};

/** Tồn nông sản đọc từ API của module thu hoạch; bán hàng làm tồn giảm nên làm mới key này sau khi lưu. */
export const produceStockKeys = {
  all: ["produce-stock"] as const,
  season: (seasonId: string) => [...produceStockKeys.all, "season", seasonId] as const,
};

export function useCustomers(query: CustomerQuery, options: { enabled?: boolean } = {}) {
  return useQuery({
    queryKey: customerKeys.list(query),
    queryFn: () => getCustomers(query),
    placeholderData: keepPreviousData,
    enabled: options.enabled,
  });
}

export function useCustomer(id: string) {
  return useQuery({ queryKey: customerKeys.detail(id), queryFn: () => getCustomer(id), enabled: id !== "" });
}

export function useCreateCustomer() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createCustomer,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: customerKeys.all }),
  });
}

export function useUpdateCustomer(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateCustomerRequest) => updateCustomer(id, request),
    // Tên khách hàng hiện trên giao dịch bán, nên làm mới cả danh sách bán hàng.
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: customerKeys.all }),
        queryClient.invalidateQueries({ queryKey: saleKeys.all }),
      ]),
  });
}

export function useSales(query: SaleQuery) {
  return useQuery({
    queryKey: saleKeys.list(query),
    queryFn: () => getSales(query),
    placeholderData: keepPreviousData,
  });
}

export function useSale(id: string) {
  return useQuery({ queryKey: saleKeys.detail(id), queryFn: () => getSale(id) });
}

export function useProduceStock(seasonId: string) {
  return useQuery({
    queryKey: produceStockKeys.season(seasonId),
    queryFn: () => getProduceStock(seasonId),
    enabled: seasonId !== "",
  });
}

/** Bán hàng, thanh toán đổi doanh thu, công nợ (trang bán hàng, công nợ), tồn nông sản và phần tài chính của mùa vụ. */
export function useInvalidateSales() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: saleKeys.all }),
      queryClient.invalidateQueries({ queryKey: produceStockKeys.all }),
      queryClient.invalidateQueries({ queryKey: seasonKeys.all }),
    ]);
}

export function useCreateSale() {
  const invalidate = useInvalidateSales();
  return useMutation({ mutationFn: createSale, onSuccess: invalidate });
}
