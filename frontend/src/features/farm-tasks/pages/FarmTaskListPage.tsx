import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { PermissionGate } from "@/components/common/PermissionGate";
import { buttonVariants } from "@/components/ui/button";
import {
  type FarmTaskFilterState,
  FarmTaskListSection,
  type TaskStatusFilterValue,
} from "@/features/farm-tasks/components/FarmTaskListSection";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { farmTaskStatuses } from "@/lib/enums";

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

export function FarmTaskListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const { data: user } = useCurrentUser();
  const isWorker = user?.role === "Worker";
  const canCreate = user !== undefined && canAccess(user, ACCESS.household);
  const mineOnly = isWorker || searchParams.get("mine") === "true";

  const state: FarmTaskFilterState = {
    page: parsePositiveInt(searchParams.get("page")),
    search: searchParams.get("search") ?? undefined,
    seasonId: searchParams.get("seasonId") ?? undefined,
    assigneeId: searchParams.get("assigneeId") ?? undefined,
    statusFilter: parseStatusFilter(searchParams),
    from: searchParams.get("from") ?? undefined,
    to: searchParams.get("to") ?? undefined,
  };

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

      <FarmTaskListSection
        mineOnly={mineOnly}
        isWorker={isWorker}
        controlledState={state}
        onControlledChange={handleStateChange}
      />
    </div>
  );
}
