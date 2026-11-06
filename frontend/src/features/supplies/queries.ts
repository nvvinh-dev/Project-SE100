import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createSupply, getSupplies, getSupply, updateSupply } from "@/features/supplies/api";
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
  return useQuery({ queryKey: supplyKeys.detail(id), queryFn: () => getSupply(id) });
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
