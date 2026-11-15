import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { correctHarvest, createHarvest, getHarvest, getHarvests, voidHarvest } from "@/features/harvests/api";
import type { CorrectHarvestRecordRequest, HarvestRecordQuery, VoidHarvestRecordRequest } from "@/features/harvests/types";
import { seasonKeys } from "@/features/seasons/queries";

export const harvestKeys = {
  all: ["harvests"] as const,
  list: (query: HarvestRecordQuery) => [...harvestKeys.all, "list", query] as const,
  detail: (id: string) => [...harvestKeys.all, "detail", id] as const,
};

export const produceStockKeys = {
  all: ["produce-stock"] as const,
};

export function useHarvests(query: HarvestRecordQuery) {
  return useQuery({
    queryKey: harvestKeys.list(query),
    queryFn: () => getHarvests(query),
    placeholderData: keepPreviousData,
  });
}

export function useHarvest(id: string) {
  return useQuery({
    queryKey: harvestKeys.detail(id),
    queryFn: () => getHarvest(id),
    enabled: Boolean(id),
  });
}

// Thu hoạch làm thay đổi sản lượng thu, độ lệch năng suất của mùa vụ và tồn nông sản.
function useInvalidateHarvests() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: harvestKeys.all }),
      queryClient.invalidateQueries({ queryKey: produceStockKeys.all }),
      queryClient.invalidateQueries({ queryKey: seasonKeys.all }),
    ]);
}

export function useCreateHarvest() {
  const invalidate = useInvalidateHarvests();
  return useMutation({ mutationFn: createHarvest, onSuccess: invalidate });
}

export function useCorrectHarvest(id: string) {
  const invalidate = useInvalidateHarvests();
  return useMutation({
    mutationFn: (request: CorrectHarvestRecordRequest) => correctHarvest(id, request),
    onSuccess: invalidate,
  });
}

export function useVoidHarvest(id: string) {
  const invalidate = useInvalidateHarvests();
  return useMutation({
    mutationFn: (request: VoidHarvestRecordRequest) => voidHarvest(id, request),
    onSuccess: invalidate,
  });
}
