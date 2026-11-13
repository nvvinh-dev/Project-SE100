import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { PermissionGate } from "@/components/common/PermissionGate";
import { buttonVariants } from "@/components/ui/button";
import {
  FarmTaskDataList,
  type FarmTaskFilterState,
  FarmTaskListSection,
  type TaskStatusFilterValue,
} from "@/features/farm-tasks/components/FarmTaskListSection";
import { useFarmTaskReminders } from "@/features/farm-tasks/queries";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { farmTaskStatuses } from "@/lib/enums";
import { cn } from "@/lib/utils";

type TaskViewTab = "overdue" | "today" | "upcoming" | "all";

function parsePositiveInt(value: string | null): number | undefined {
  if (!value) {
    return undefined;
  }
  const parsed = Number.parseInt(value, 10);
  return Number.isFinite(parsed) && parsed >= 1 ? parsed : undefined;
}

function parseStatusFilter(params: URLSearchParams): TaskStatusFilterValue | undefined {
  if (params.get("isOverdue") === "true") {
    return "Overdue";
  }
  const status = params.get("status");
  if (status && (farmTaskStatuses as readonly string[]).includes(status)) {
    return status as TaskStatusFilterValue;
  }
  return undefined;
}

function parseTab(value: string | null): TaskViewTab {
  if (value === "overdue" || value === "today" || value === "upcoming") {
    return value;
  }
  return "all";
}

export function FarmTaskListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: user } = useCurrentUser();
  const isWorker = user?.role === "Worker";
  const canCreate = user !== undefined && canAccess(user, ACCESS.household);
  const mineOnly = isWorker || searchParams.get("mine") === "true";
  const activeTab = parseTab(searchParams.get("tab"));

  const remindersQuery = useFarmTaskReminders(mineOnly, user !== undefined);

  const state: FarmTaskFilterState = {
    page: parsePositiveInt(searchParams.get("page")),
    search: searchParams.get("search") ?? undefined,
    seasonId: searchParams.get("seasonId") ?? undefined,
    assigneeId: searchParams.get("assigneeId") ?? undefined,
    statusFilter: parseStatusFilter(searchParams),
    from: searchParams.get("from") ?? undefined,
    to: searchParams.get("to") ?? undefined,
  };

  function selectTab(nextTab: TaskViewTab) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);
      if (nextTab === "all") {
        next.delete("tab");
      } else {
        next.set("tab", nextTab);
      }
      return next;
    });
  }

  function handleStateChange(changes: Partial<FarmTaskFilterState>) {
    setSearchParams((prev) => {
      const next = new URLSearchParams(prev);

      if ("search" in changes) {
        if (changes.search) next.set("search", changes.search);
        else next.delete("search");
      }
      if ("seasonId" in changes) {
        if (changes.seasonId) next.set("seasonId", changes.seasonId);
        else next.delete("seasonId");
      }
      if ("assigneeId" in changes) {
        if (changes.assigneeId) next.set("assigneeId", changes.assigneeId);
        else next.delete("assigneeId");
      }
      if ("statusFilter" in changes) {
        next.delete("status");
        next.delete("isOverdue");
        if (changes.statusFilter === "Overdue") {
          next.set("isOverdue", "true");
        } else if (changes.statusFilter) {
          next.set("status", changes.statusFilter);
        }
      }
      if ("from" in changes) {
        if (changes.from) next.set("from", changes.from);
        else next.delete("from");
      }
      if ("to" in changes) {
        if (changes.to) next.set("to", changes.to);
        else next.delete("to");
      }

      if ("page" in changes) {
        if (changes.page && changes.page > 1) next.set("page", String(changes.page));
        else next.delete("page");
      } else {
        next.delete("page");
      }

      return next;
    });
  }

  const createHref = state.seasonId ? `/tasks/new?seasonId=${state.seasonId}` : "/tasks/new";
  const reminders = remindersQuery.data;
  const upcomingDays = reminders?.upcomingDays ?? 3;

  const tabs: { key: TaskViewTab; label: string; count?: number; urgent?: boolean }[] = [
    {
      key: "overdue",
      label: "Trễ hạn",
      count: reminders?.overdueCount,
      urgent: (reminders?.overdueCount ?? 0) > 0,
    },
    { key: "today", label: "Hôm nay", count: reminders?.todayCount },
    { key: "upcoming", label: `${upcomingDays} ngày tới`, count: reminders?.upcomingCount },
    { key: "all", label: "Tất cả" },
  ];

  const reminderItems =
    activeTab === "overdue"
      ? (reminders?.overdue ?? [])
      : activeTab === "today"
        ? (reminders?.today ?? [])
        : activeTab === "upcoming"
          ? (reminders?.upcoming ?? [])
          : [];

  const reminderEmptyMessage =
    activeTab === "overdue"
      ? "Không có công việc trễ hạn."
      : activeTab === "today"
        ? "Hôm nay không có công việc nào đến hạn."
        : `Không có công việc nào trong ${upcomingDays} ngày tới.`;

  return (
    <div className="flex flex-col gap-4">
      <PageHeader
        title={isWorker ? "Việc của tôi" : "Công việc canh tác"}
        action={
          <PermissionGate allowed={canCreate}>
            <Link to={createHref} className={buttonVariants()}>
              <PlusIcon data-icon="inline-start" />
              Thêm công việc
            </Link>
          </PermissionGate>
        }
      />

      <div role="tablist" aria-label="Nhóm nhắc việc" className="flex gap-2 overflow-x-auto pb-1">
        {tabs.map((tab) => {
          const isActive = activeTab === tab.key;
          return (
            <button
              key={tab.key}
              type="button"
              role="tab"
              aria-selected={isActive}
              onClick={() => selectTab(tab.key)}
              className={cn(
                "inline-flex min-h-11 shrink-0 items-center gap-1.5 rounded-md border px-3 text-sm font-medium transition-colors",
                isActive
                  ? "border-primary bg-primary text-primary-foreground"
                  : tab.urgent
                    ? "border-destructive/50 text-destructive hover:bg-destructive/5"
                    : "bg-background hover:bg-muted",
              )}
            >
              <span>{tab.label}</span>
              {tab.count !== undefined && (
                <span
                  className={cn(
                    "rounded-full px-1.5 py-0.5 text-xs",
                    isActive ? "bg-primary-foreground/20 text-primary-foreground" : "bg-muted text-foreground",
                  )}
                >
                  {tab.count}
                </span>
              )}
            </button>
          );
        })}
      </div>

      {activeTab === "all" ? (
        <FarmTaskListSection
          mineOnly={mineOnly}
          isWorker={isWorker}
          controlledState={state}
          onControlledChange={handleStateChange}
        />
      ) : remindersQuery.isPending ? (
        <LoadingState variant="list" />
      ) : remindersQuery.isError ? (
        <ErrorAlert error={remindersQuery.error} onRetry={() => void remindersQuery.refetch()} />
      ) : reminderItems.length === 0 ? (
        <EmptyState message={reminderEmptyMessage} />
      ) : (
        <FarmTaskDataList items={reminderItems} isWorker={isWorker} />
      )}
    </div>
  );
}
