import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createStockTransaction, createSupply, getSupplies, getSupply, updateSupply } from "@/features/supplies/api";
import type { SupplyQuery, UpdateSupplyRequest } from "@/features/supplies/types";

export const supplyKeys = {
  all: ["supplies"] as const,
  list: (query: SupplyQuery) => [...supplyKeys.all, "list", query] as const,
  detail: (id: string) => [...supplyKeys.all, "detail", id] as const,
};

export function useSupplies(query: SupplyQuery) {
  return useQuery({
    queryKey: supplyKeys.list(query),
    queryFn: () => getSupplies(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}

export function useSupply(id: string) {
  // Form kho gọi khi chưa chọn vật tư thì chưa có gì để tải.
  return useQuery({ queryKey: supplyKeys.detail(id), queryFn: () => getSupply(id), enabled: id !== "" });
}

export function useCreateSupply() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createSupply,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: supplyKeys.all }),
  });
}

export function useUpdateSupply(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateSupplyRequest) => updateSupply(id, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: supplyKeys.all }),
  });
}

export function useCreateStockTransaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createStockTransaction,
    // Tồn, giá vốn của vật tư đều đổi theo giao dịch kho.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: supplyKeys.all }),
  });
}
