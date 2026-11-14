import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createCrop, getCrop, getCrops, updateCrop } from "@/features/crops/api";
import type { CropQuery, UpdateCropRequest } from "@/features/crops/types";
import { seasonKeys } from "@/features/seasons/queries";

export const cropKeys = {
  all: ["crops"] as const,
  list: (query: CropQuery) => [...cropKeys.all, "list", query] as const,
  detail: (id: string) => [...cropKeys.all, "detail", id] as const,
};

export function useCrops(query: CropQuery) {
  return useQuery({
    queryKey: cropKeys.list(query),
    queryFn: () => getCrops(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}

export function useCrop(id: string) {
  return useQuery({ queryKey: cropKeys.detail(id), queryFn: () => getCrop(id) });
}

// Ô chọn cây khi tạo mùa vụ giữ danh sách cây đang sử dụng riêng: tạo, sửa cây thì làm mới cả danh sách đó.
function invalidateCrops(queryClient: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: cropKeys.all }),
    queryClient.invalidateQueries({ queryKey: seasonKeys.activeCrops() }),
  ]);
}

export function useCreateCrop() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createCrop,
    onSuccess: () => invalidateCrops(queryClient),
  });
}

export function useUpdateCrop(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateCropRequest) => updateCrop(id, request),
    onSuccess: () => invalidateCrops(queryClient),
  });
}
