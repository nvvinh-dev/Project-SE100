import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  cancelFarmTask,
  cancelRecurrenceGroup,
  createFarmTask,
  createRecurringFarmTasks,
  getFarmTask,
  getFarmTaskAssignees,
  getFarmTasks,
  updateFarmTask,
} from "@/features/farm-tasks/api";
import type {
  CancelFarmTaskRequest,
  CreateFarmTaskRequest,
  CreateRecurringFarmTasksRequest,
  FarmTaskQuery,
  UpdateFarmTaskRequest,
} from "@/features/farm-tasks/types";
import { seasonKeys } from "@/features/seasons/queries";

export const farmTaskKeys = {
  all: ["farm-tasks"] as const,
  list: (query: FarmTaskQuery) => [...farmTaskKeys.all, "list", query] as const,
  detail: (id: string) => [...farmTaskKeys.all, "detail", id] as const,
  assignees: (seasonId?: string) => [...farmTaskKeys.all, "assignees", seasonId ?? "all"] as const,
};

export function useFarmTasks(query: FarmTaskQuery, enabled = true) {
  return useQuery({
    queryKey: farmTaskKeys.list(query),
    queryFn: () => getFarmTasks(query),
    placeholderData: keepPreviousData,
    enabled,
  });
}

export function useFarmTaskAssignees(seasonId?: string, enabled = true) {
  return useQuery({
    queryKey: farmTaskKeys.assignees(seasonId),
    queryFn: () => getFarmTaskAssignees(seasonId),
    enabled,
  });
}

export function useFarmTask(id: string) {
  return useQuery({
    queryKey: farmTaskKeys.detail(id),
    queryFn: () => getFarmTask(id),
    enabled: id !== "",
  });
}

export function useCreateFarmTask() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateFarmTaskRequest) => createFarmTask(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: farmTaskKeys.all });
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}

export function useCreateRecurringFarmTasks() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CreateRecurringFarmTasksRequest) => createRecurringFarmTasks(request),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: farmTaskKeys.all });
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}

export function useUpdateFarmTask(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: UpdateFarmTaskRequest) => updateFarmTask(id, request),
    onSuccess: (updated) => {
      queryClient.setQueryData(farmTaskKeys.detail(id), updated);
      void queryClient.invalidateQueries({ queryKey: farmTaskKeys.all });
    },
  });
}

export function useCancelFarmTask(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (request: CancelFarmTaskRequest) => cancelFarmTask(id, request),
    onSuccess: (updated) => {
      queryClient.setQueryData(farmTaskKeys.detail(id), updated);
      void queryClient.invalidateQueries({ queryKey: farmTaskKeys.all });
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}

export function useCancelRecurrenceGroup() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (recurrenceGroupId: string) => cancelRecurrenceGroup(recurrenceGroupId),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: farmTaskKeys.all });
      void queryClient.invalidateQueries({ queryKey: seasonKeys.all });
    },
  });
}
