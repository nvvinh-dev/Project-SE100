import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  createStockTransaction,
  createSupply,
  getStockTransactions,
  getSupplies,
  getSupply,
  updateSupply,
  voidStockTransaction,
} from "@/features/supplies/api";
import { seasonKeys } from "@/features/seasons/queries";
import type { StockTransactionQuery, SupplyQuery, UpdateSupplyRequest, VoidStockTransactionRequest } from "@/features/supplies/types";

export const supplyKeys = {
  all: ["supplies"] as const,
  list: (query: SupplyQuery) => [...supplyKeys.all, "list", query] as const,
  detail: (id: string) => [...supplyKeys.all, "detail", id] as const,
};

export const stockTransactionKeys = {
  all: ["stock-transactions"] as const,
  list: (query: StockTransactionQuery) => [...stockTransactionKeys.all, "list", query] as const,
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
    // Tồn, giá vốn của vật tư và lịch sử kho đều đổi theo giao dịch kho; xuất kho còn đổi chi phí vật tư của mùa vụ.
    onSuccess: (_data, request) =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: supplyKeys.all }),
        queryClient.invalidateQueries({ queryKey: stockTransactionKeys.all }),
        ...(request.type === "Out" ? [queryClient.invalidateQueries({ queryKey: seasonKeys.all })] : []),
      ]),
  });
}

export function useStockTransactions(query: StockTransactionQuery) {
  return useQuery({
    queryKey: stockTransactionKeys.list(query),
    queryFn: () => getStockTransactions(query),
    placeholderData: keepPreviousData,
  });
}

export function useVoidStockTransaction() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: VoidStockTransactionRequest }) => voidStockTransaction(id, request),
    // Hủy giao dịch đổi tồn, giá vốn của vật tư và dòng trong lịch sử kho.
    onSuccess: () =>
      Promise.all([
        queryClient.invalidateQueries({ queryKey: supplyKeys.all }),
        queryClient.invalidateQueries({ queryKey: stockTransactionKeys.all }),
      ]),
  });
}
