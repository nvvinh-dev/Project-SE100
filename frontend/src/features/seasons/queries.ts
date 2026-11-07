import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createSeason, getSeason, getSeasonOptions, getSeasons, updateSeason } from "@/features/seasons/api";
import type { SeasonOptionQuery, SeasonQuery, UpdateSeasonRequest } from "@/features/seasons/types";

export const seasonKeys = {
  all: ["seasons"] as const,
  list: (query: SeasonQuery) => [...seasonKeys.all, "list", query] as const,
  options: (query: SeasonOptionQuery) => [...seasonKeys.all, "options", query] as const,
  detail: (id: string) => [...seasonKeys.all, "detail", id] as const,
};

// Lấy danh sách mùa vụ, giữ trang cũ trong lúc tải mới để không nháy giao diện (FR-SEA-11).
export function useSeasons(query: SeasonQuery) {
  return useQuery({
    queryKey: seasonKeys.list(query),
    queryFn: () => getSeasons(query),
    placeholderData: keepPreviousData,
  });
}

// Lấy danh sách mùa vụ cho ô chọn theo loại thao tác (FR-SEA-11).
export function useSeasonOptions(query: SeasonOptionQuery) {
  return useQuery({
    queryKey: seasonKeys.options(query),
    queryFn: () => getSeasonOptions(query),
  });
}

// Chi tiết mùa vụ.
export function useSeason(id: string) {
  return useQuery({
    queryKey: seasonKeys.detail(id),
    queryFn: () => getSeason(id),
    enabled: Boolean(id),
  });
}

// Mutation tạo mùa vụ (FR-SEA-01).
export function useCreateSeason() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createSeason,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: seasonKeys.all }),
  });
}

// Mutation sửa mùa vụ (FR-SEA-07).
export function useUpdateSeason(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateSeasonRequest) => updateSeason(id, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: seasonKeys.all }),
  });
}
