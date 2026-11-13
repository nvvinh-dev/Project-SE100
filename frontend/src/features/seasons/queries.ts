import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  assignSeason,
  createSeason,
  createSeasonNote,
  createSeasonSupplyPlan,
  deleteSeasonSupplyPlan,
  getActiveCrops,
  getHouseholdMembers,
  getSeason,
  getSeasonAssignments,
  getSeasonDetail,
  getSeasonNotes,
  getSeasonOptions,
  getSeasons,
  getSeasonSupplyPlans,
  startSeason,
  unassignSeason,
  updateSeason,
  updateSeasonNote,
  updateSeasonSupplyPlan,
} from "@/features/seasons/api";
import type {
  CreateSeasonAssignmentRequest,
  CreateSeasonNoteRequest,
  CreateSeasonRequest,
  CreateSeasonSupplyPlanRequest,
  SeasonOptionQuery,
  SeasonQuery,
  UnassignSeasonAssignmentRequest,
  UpdateSeasonNoteRequest,
  UpdateSeasonRequest,
  UpdateSeasonSupplyPlanRequest,
} from "@/features/seasons/types";

export const seasonKeys = {
  all: ["seasons"] as const,
  list: (query: SeasonQuery) => [...seasonKeys.all, "list", query] as const,
  options: (query: SeasonOptionQuery) => [...seasonKeys.all, "options", query] as const,
  detail: (id: string) => [...seasonKeys.all, "detail", id] as const,
  detailFull: (id: string) => [...seasonKeys.all, "detailFull", id] as const,
  assignments: (seasonId: string) => [...seasonKeys.all, "assignments", seasonId] as const,
  notes: (seasonId: string) => [...seasonKeys.all, "notes", seasonId] as const,
  supplyPlans: (seasonId: string) => [...seasonKeys.all, "supplyPlans", seasonId] as const,
  activeCrops: () => [...seasonKeys.all, "activeCrops"] as const,
  members: () => ["householdMembers"] as const,
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

// Danh sách cây trồng đang hoạt động để chọn (FR-SEA-01, FR-SEA-02).
export function useActiveCrops() {
  return useQuery({
    queryKey: seasonKeys.activeCrops(),
    queryFn: () => getActiveCrops(),
    staleTime: 5 * 60 * 1000,
  });
}

// Thêm mùa vụ mới (FR-SEA-01).
export function useCreateSeason() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSeasonRequest) => createSeason(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}

// Sửa mùa vụ (FR-SEA-07).
export function useUpdateSeason(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateSeasonRequest) => updateSeason(id, request),
    onSuccess: (data) => {
      queryClient.setQueryData(seasonKeys.detail(id), data);
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}

// Chi tiết mùa vụ đầy đủ (FR-SEA-12).
export function useSeasonDetail(id: string) {
  return useQuery({
    queryKey: seasonKeys.detailFull(id),
    queryFn: () => getSeasonDetail(id),
    enabled: Boolean(id),
  });
}

// Bắt đầu mùa vụ (FR-SEA-08).
export function useStartSeason(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => startSeason(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}

// Danh sách phân công mùa vụ (FR-SEA-06).
export function useSeasonAssignments(seasonId: string) {
  return useQuery({
    queryKey: seasonKeys.assignments(seasonId),
    queryFn: () => getSeasonAssignments(seasonId),
    enabled: Boolean(seasonId),
  });
}

// Phân công người vào mùa vụ (FR-SEA-06).
export function useAssignSeason(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSeasonAssignmentRequest) => assignSeason(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.assignments(seasonId) });
      void queryClient.invalidateQueries({ queryKey: seasonKeys.detailFull(seasonId) });
    },
  });
}

// Thu hồi phân công (FR-SEA-06, BR-USR-12, BR-GEN-14).
export function useUnassignSeason(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request?: UnassignSeasonAssignmentRequest }) =>
      unassignSeason(id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.assignments(seasonId) });
      void queryClient.invalidateQueries({ queryKey: seasonKeys.detailFull(seasonId) });
    },
  });
}

// Danh sách ghi chú kinh nghiệm của mùa vụ (FR-SEA-13).
export function useSeasonNotes(seasonId: string) {
  return useQuery({
    queryKey: seasonKeys.notes(seasonId),
    queryFn: () => getSeasonNotes(seasonId),
    enabled: Boolean(seasonId),
  });
}

// Thêm ghi chú kinh nghiệm (FR-SEA-13, BR-SEA-11).
export function useCreateSeasonNote(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSeasonNoteRequest) => createSeasonNote(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.notes(seasonId) });
    },
  });
}

// Sửa ghi chú kinh nghiệm (FR-SEA-13, BR-SEA-11).
export function useUpdateSeasonNote(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateSeasonNoteRequest }) =>
      updateSeasonNote(id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.notes(seasonId) });
    },
  });
}

// Lấy danh sách thành viên hộ để chọn phân công.
export function useHouseholdMembers() {
  return useQuery({
    queryKey: seasonKeys.members(),
    queryFn: () => getHouseholdMembers(),
    staleTime: 5 * 60 * 1000,
  });
}

// Lấy danh sách vật tư dự kiến của mùa vụ (FR-SEA-04).
export function useSeasonSupplyPlans(seasonId: string) {
  return useQuery({
    queryKey: seasonKeys.supplyPlans(seasonId),
    queryFn: () => getSeasonSupplyPlans(seasonId),
    enabled: Boolean(seasonId),
  });
}

// Khai báo vật tư dự kiến cho mùa vụ (FR-SEA-04).
export function useCreateSeasonSupplyPlan(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateSeasonSupplyPlanRequest) => createSeasonSupplyPlan(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.supplyPlans(seasonId) });
    },
  });
}

// Cập nhật lượng vật tư dự kiến của mùa vụ (FR-SEA-04).
export function useUpdateSeasonSupplyPlan(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, request }: { id: string; request: UpdateSeasonSupplyPlanRequest }) =>
      updateSeasonSupplyPlan(id, request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.supplyPlans(seasonId) });
    },
  });
}

// Xóa một dòng vật tư dự kiến (FR-SEA-04).
export function useDeleteSeasonSupplyPlan(seasonId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => deleteSeasonSupplyPlan(id),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: seasonKeys.supplyPlans(seasonId) });
    },
  });
}

