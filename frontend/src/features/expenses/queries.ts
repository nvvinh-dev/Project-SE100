import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { correctExpense, createExpense, getExpense, getExpenses, voidExpense } from "@/features/expenses/api";
import type { CorrectExpenseRequest, ExpenseQuery, VoidExpenseRequest } from "@/features/expenses/types";
import { seasonKeys } from "@/features/seasons/queries";

export const expenseKeys = {
  all: ["expenses"] as const,
  list: (query: ExpenseQuery) => [...expenseKeys.all, "list", query] as const,
  detail: (id: string) => [...expenseKeys.all, "detail", id] as const,
};

export function useExpenses(query: ExpenseQuery) {
  return useQuery({
    queryKey: expenseKeys.list(query),
    queryFn: () => getExpenses(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}

export function useExpense(id: string) {
  return useQuery({ queryKey: expenseKeys.detail(id), queryFn: () => getExpense(id) });
}

// Chi phí ghi tay đổi số liệu tài chính của mùa vụ (chi phí, lợi nhuận) nên tải lại cả dữ liệu mùa vụ.
function useInvalidateExpenses() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: expenseKeys.all }),
      queryClient.invalidateQueries({ queryKey: seasonKeys.all }),
    ]);
}

export function useCreateExpense() {
  const invalidate = useInvalidateExpenses();
  return useMutation({ mutationFn: createExpense, onSuccess: invalidate });
}

export function useCorrectExpense(id: string) {
  const invalidate = useInvalidateExpenses();
  return useMutation({
    mutationFn: (request: CorrectExpenseRequest) => correctExpense(id, request),
    onSuccess: invalidate,
  });
}

export function useVoidExpense(id: string) {
  const invalidate = useInvalidateExpenses();
  return useMutation({
    mutationFn: (request: VoidExpenseRequest) => voidExpense(id, request),
    onSuccess: invalidate,
  });
}
