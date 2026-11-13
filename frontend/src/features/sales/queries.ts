import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createCustomer, getCustomer, getCustomers, updateCustomer } from "@/features/sales/api";
import type { CustomerQuery, UpdateCustomerRequest } from "@/features/sales/types";

export const customerKeys = {
  all: ["customers"] as const,
  list: (query: CustomerQuery) => [...customerKeys.all, "list", query] as const,
  detail: (id: string) => [...customerKeys.all, "detail", id] as const,
};

export function useCustomers(query: CustomerQuery) {
  return useQuery({
    queryKey: customerKeys.list(query),
    queryFn: () => getCustomers(query),
    placeholderData: keepPreviousData,
  });
}

export function useCustomer(id: string) {
  return useQuery({ queryKey: customerKeys.detail(id), queryFn: () => getCustomer(id) });
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
    onSuccess: () => queryClient.invalidateQueries({ queryKey: customerKeys.all }),
  });
}
