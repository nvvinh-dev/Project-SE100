import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { createPlot, getPlot, getPlots, updatePlot } from "@/features/plots/api";
import type { PlotQuery, UpdatePlotRequest } from "@/features/plots/types";

export const plotKeys = {
  all: ["plots"] as const,
  list: (query: PlotQuery) => [...plotKeys.all, "list", query] as const,
  detail: (id: string) => [...plotKeys.all, "detail", id] as const,
};

export function usePlots(query: PlotQuery) {
  return useQuery({
    queryKey: plotKeys.list(query),
    queryFn: () => getPlots(query),
    // Giữ trang cũ trong lúc tải trang mới, để danh sách không nháy về khung xám mỗi lần lọc, chuyển trang.
    placeholderData: keepPreviousData,
  });
}

export function usePlot(id: string) {
  return useQuery({ queryKey: plotKeys.detail(id), queryFn: () => getPlot(id) });
}

export function useCreatePlot() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: createPlot,
    // Danh sách, số thửa ở trang "Hôm nay" đều nằm dưới plotKeys.all.
    onSuccess: () => queryClient.invalidateQueries({ queryKey: plotKeys.all }),
  });
}

export function useUpdatePlot(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdatePlotRequest) => updatePlot(id, request),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: plotKeys.all }),
  });
}
