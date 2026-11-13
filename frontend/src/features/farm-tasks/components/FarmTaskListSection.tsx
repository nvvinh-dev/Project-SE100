import { RepeatIcon } from "lucide-react";
import { useCallback, useState } from "react";
import { DataList, type DataListColumn } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { Pagination } from "@/components/common/Pagination";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useFarmTaskAssignees, useFarmTasks } from "@/features/farm-tasks/queries";
import { formatFarmTaskTitle } from "@/features/farm-tasks/task-format";
import type { FarmTaskListItem, FarmTaskQuery } from "@/features/farm-tasks/types";
import { SeasonSelect } from "@/features/seasons/components/SeasonSelect";
import { farmTaskStatusLabels } from "@/lib/enum-labels";
import { type FarmTaskStatus, farmTaskStatuses } from "@/lib/enums";
import { formatDate } from "@/lib/format";

export type TaskStatusFilterValue = FarmTaskStatus | "Overdue";

export interface FarmTaskFilterState {
  page?: number;
  search?: string;
  seasonId?: string;
  assigneeId?: string;
  statusFilter?: TaskStatusFilterValue;
  from?: string;
  to?: string;
}

interface FarmTaskListSectionProps {
  /** Khóa danh sách vào một mùa vụ cụ thể (khi nhúng trong chi tiết mùa vụ). */
  fixedSeasonId?: string;
  /** Giới hạn vào công việc giao cho chính người đang đăng nhập. */
  mineOnly?: boolean;
  /** Vai trò Nhân công: ẩn bộ lọc người thực hiện và mùa vụ không cần thiết. */
  isWorker?: boolean;
  /** Bộ lọc ngày hoặc trễ hạn cố định từ tab nhắc việc. */
  fixedConstraints?: Pick<FarmTaskQuery, "isOverdue" | "from" | "to">;
  /** Trạng thái bộ lọc điều khiển từ URL ở trang `/tasks`, bỏ trống khi dùng state nội bộ. */
  controlledState?: FarmTaskFilterState;
  onControlledChange?: (changes: Partial<FarmTaskFilterState>) => void;
  emptyMessage?: string;
}

export function FarmTaskListSection({
  fixedSeasonId,
  mineOnly = false,
  isWorker = false,
  fixedConstraints,
  controlledState,
  onControlledChange,
  emptyMessage = "Chưa có công việc nào. Tạo công việc mới để theo dõi lịch canh tác.",
}: FarmTaskListSectionProps) {
  const [localState, setLocalState] = useState<FarmTaskFilterState>({});
  const state = controlledState ?? localState;

  const updateState = useCallback(
    (changes: Partial<FarmTaskFilterState>) => {
      if (onControlledChange) {
        onControlledChange(changes);
        return;
      }
      setLocalState((prev) => {
        const next = { ...prev, ...changes };
        if (!("page" in changes)) {
          next.page = undefined;
        }
        return next;
      });
    },
    [onControlledChange],
  );

  const effectiveSeasonId = fixedSeasonId ?? state.seasonId;
  const isOverdueSelected = state.statusFilter === "Overdue";
  const statusParam: FarmTaskStatus | undefined =
    state.statusFilter && state.statusFilter !== "Overdue" ? state.statusFilter : undefined;

  const query: FarmTaskQuery = {
    page: state.page ?? 1,
    pageSize: 20,
    search: state.search || undefined,
    seasonId: effectiveSeasonId || undefined,
    assigneeId: !mineOnly && !isWorker ? state.assigneeId || undefined : undefined,
    status: statusParam,
    isOverdue: fixedConstraints?.isOverdue ?? (isOverdueSelected ? true : undefined),
    from: fixedConstraints?.from ?? (state.from || undefined),
    to: fixedConstraints?.to ?? (state.to || undefined),
    mine: mineOnly || undefined,
  };

  const tasksQuery = useFarmTasks(query);
  const assigneesQuery = useFarmTaskAssignees(effectiveSeasonId || undefined, !isWorker && !mineOnly);
  const assignees = assigneesQuery.data ?? [];

  const handleSeasonFilterChange = useCallback(
    (nextSeasonId: string) => {
      updateState({ seasonId: nextSeasonId || undefined, assigneeId: undefined });
    },
    [updateState],
  );

  const activeFilters: ActiveFilter[] = [];
  if (!fixedSeasonId && state.seasonId) {
    activeFilters.push({
      key: "seasonId",
      label: "Đang lọc theo mùa vụ",
      onRemove: () => updateState({ seasonId: undefined, assigneeId: undefined }),
    });
  }
  if (state.statusFilter) {
    activeFilters.push({
      key: "statusFilter",
      label:
        state.statusFilter === "Overdue"
          ? "Trạng thái: Trễ hạn"
          : `Trạng thái: ${farmTaskStatusLabels[state.statusFilter]}`,
      onRemove: () => updateState({ statusFilter: undefined }),
    });
  }
  if (!mineOnly && !isWorker && state.assigneeId) {
    const assigneeName = assignees.find((a) => a.userId === state.assigneeId)?.fullName ?? "Đã chọn";
    activeFilters.push({
      key: "assigneeId",
      label: `Người thực hiện: ${assigneeName}`,
      onRemove: () => updateState({ assigneeId: undefined }),
    });
  }
  if (!fixedConstraints?.from && state.from) {
    activeFilters.push({
      key: "from",
      label: `Từ ${formatDate(state.from)}`,
      onRemove: () => updateState({ from: undefined }),
    });
  }
  if (!fixedConstraints?.to && state.to) {
    activeFilters.push({
      key: "to",
      label: `Đến ${formatDate(state.to)}`,
      onRemove: () => updateState({ to: undefined }),
    });
  }

  const isFiltered = activeFilters.length > 0 || Boolean(state.search);

  return (
    <div className="flex flex-col gap-4">
      <SearchFilterBar
        search={state.search ?? ""}
        onSearchChange={(search) => updateState({ search: search || undefined })}
        searchPlaceholder={isWorker ? "Tìm theo loại việc, ghi chú, thửa" : "Tìm theo loại việc, thửa, người làm"}
        activeFilters={activeFilters}
        filters={
          <>
            {!fixedSeasonId && !isWorker && (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="filter-task-season">Mùa vụ</Label>
                <SeasonSelect
                  id="filter-task-season"
                  value={state.seasonId ?? ""}
                  onChange={handleSeasonFilterChange}
                  emptyLabel="Tất cả mùa vụ"
                />
              </div>
            )}

            <div className="flex flex-col gap-1.5">
              <Label htmlFor="filter-task-status">Trạng thái</Label>
              <NativeSelect
                id="filter-task-status"
                className="w-full"
                value={state.statusFilter ?? ""}
                onChange={(e) =>
                  updateState({
                    statusFilter: (e.target.value || undefined) as TaskStatusFilterValue | undefined,
                  })
                }
              >
                <NativeSelectOption value="">Tất cả trạng thái</NativeSelectOption>
                <NativeSelectOption value="Overdue">Trễ hạn</NativeSelectOption>
                {farmTaskStatuses.map((status) => (
                  <NativeSelectOption key={status} value={status}>
                    {farmTaskStatusLabels[status]}
                  </NativeSelectOption>
                ))}
              </NativeSelect>
            </div>

            {!isWorker && !mineOnly && (
              <div className="flex flex-col gap-1.5">
                <Label htmlFor="filter-task-assignee">Người thực hiện</Label>
                <NativeSelect
                  id="filter-task-assignee"
                  className="w-full"
                  value={state.assigneeId ?? ""}
                  onChange={(e) => updateState({ assigneeId: e.target.value || undefined })}
                >
                  <NativeSelectOption value="">Tất cả người thực hiện</NativeSelectOption>
                  {assignees.map((member) => (
                    <NativeSelectOption key={member.userId} value={member.userId}>
                      {member.fullName}
                    </NativeSelectOption>
                  ))}
                </NativeSelect>
              </div>
            )}

            {!fixedConstraints?.from && !fixedConstraints?.to && (
              <div className="grid gap-3 sm:grid-cols-2">
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="filter-task-from">Từ ngày</Label>
                  <Input
                    id="filter-task-from"
                    type="date"
                    value={state.from ?? ""}
                    onChange={(e) => updateState({ from: e.target.value || undefined })}
                  />
                </div>
                <div className="flex flex-col gap-1.5">
                  <Label htmlFor="filter-task-to">Đến ngày</Label>
                  <Input
                    id="filter-task-to"
                    type="date"
                    value={state.to ?? ""}
                    onChange={(e) => updateState({ to: e.target.value || undefined })}
                  />
                </div>
              </div>
            )}
          </>
        }
      />

      {tasksQuery.isPending ? (
        <LoadingState variant="list" />
      ) : tasksQuery.isError ? (
        <ErrorAlert error={tasksQuery.error} onRetry={() => void tasksQuery.refetch()} />
      ) : tasksQuery.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState
            onClearFilters={() =>
              updateState({
                search: undefined,
                seasonId: undefined,
                assigneeId: undefined,
                statusFilter: undefined,
                from: undefined,
                to: undefined,
              })
            }
          />
        ) : (
          <EmptyState message={emptyMessage} />
        )
      ) : (
        <>
          <FarmTaskDataList
            items={tasksQuery.data.items}
            includeSeason={!fixedSeasonId}
            isWorker={isWorker}
          />
          <Pagination meta={tasksQuery.data.meta} onPageChange={(page) => updateState({ page })} />
        </>
      )}
    </div>
  );
}

interface FarmTaskDataListProps {
  items: FarmTaskListItem[];
  includeSeason?: boolean;
  isWorker?: boolean;
}

export function FarmTaskDataList({ items, includeSeason = true, isWorker = false }: FarmTaskDataListProps) {
  const columns: DataListColumn<FarmTaskListItem>[] = [
    {
      header: "Ngày dự kiến",
      cell: (item) => <DateText value={item.scheduledDate} />,
    },
    {
      header: "Công việc",
      cell: (item) => (
        <div className="flex items-center gap-1.5 font-medium">
          <span>{formatFarmTaskTitle(item)}</span>
          {item.recurrenceGroupId && <RepeatIcon className="h-3.5 w-3.5 text-muted-foreground" aria-label="Công việc lặp" />}
        </div>
      ),
    },
    ...(includeSeason
      ? [
          {
            header: "Mùa vụ",
            cell: (item: FarmTaskListItem) => `${item.plotCode} · ${item.cropName}`,
          },
        ]
      : []),
    ...(!isWorker
      ? [
          {
            header: "Người thực hiện",
            cell: (item: FarmTaskListItem) => item.assigneeName ?? "Chưa giao",
          },
        ]
      : []),
    {
      header: "Trạng thái",
      cell: (item) => (
        <div className="flex flex-wrap items-center gap-1.5">
          <StatusBadge kind="FarmTaskStatus" value={item.status} />
          {item.isOverdue && <StatusBadge kind="Overdue" />}
        </div>
      ),
    },
  ];

  return (
    <DataList
      items={items}
      getKey={(item) => item.id}
      getHref={(item) => `/tasks/${item.id}`}
      title={(item) => (
        <span className="flex items-center gap-1.5">
          <span>{formatFarmTaskTitle(item)}</span>
          {item.recurrenceGroupId && <RepeatIcon className="h-3.5 w-3.5 text-muted-foreground" />}
        </span>
      )}
      subtitle={(item) => {
        const parts = [formatDate(item.scheduledDate), `${item.plotCode} · ${item.cropName}`];
        if (!isWorker) {
          parts.push(item.assigneeName ?? "Chưa giao");
        }
        return parts.join(" · ");
      }}
      badge={(item) => (
        <div className="flex flex-col items-end gap-1">
          <StatusBadge kind="FarmTaskStatus" value={item.status} />
          {item.isOverdue && <StatusBadge kind="Overdue" />}
        </div>
      )}
      columns={columns}
    />
  );
}
