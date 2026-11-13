import { Link } from "react-router";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { useFarmTaskReminders } from "@/features/farm-tasks/queries";
import { formatFarmTaskTitle } from "@/features/farm-tasks/task-format";
import { cn } from "@/lib/utils";

/** Thẻ "Việc cần làm" cho trang Hôm nay (FR-TSK-08), sẵn sàng ghép ở CHUNG-02. */
export function TodayTasksCard() {
  const remindersQuery = useFarmTaskReminders(false);

  if (remindersQuery.isPending) {
    return (
      <section className="flex flex-col gap-3 rounded-lg border p-4">
        <h2 className="font-semibold">Việc cần làm</h2>
        <LoadingState variant="list" />
      </section>
    );
  }

  if (remindersQuery.isError) {
    return (
      <section className="flex flex-col gap-3 rounded-lg border p-4">
        <h2 className="font-semibold">Việc cần làm</h2>
        <ErrorAlert error={remindersQuery.error} onRetry={() => void remindersQuery.refetch()} />
      </section>
    );
  }

  const data = remindersQuery.data;
  const prioritized = [...data.overdue, ...data.today, ...data.upcoming].slice(0, 5);

  return (
    <section className="flex flex-col gap-3 rounded-lg border p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="font-semibold">Việc cần làm</h2>
        <Link to="/tasks" className={buttonVariants({ variant: "ghost", size: "sm", className: "min-h-11" })}>
          Xem tất cả
        </Link>
      </div>

      <div className="grid grid-cols-3 gap-2 text-center text-sm">
        <Link
          to="/tasks?tab=overdue"
          className={cn(
            "flex min-h-11 flex-col items-center justify-center rounded-md border px-2 py-1.5 transition-colors hover:bg-muted/50",
            data.overdueCount > 0 && "border-destructive/50 bg-destructive/5 text-destructive",
          )}
        >
          <span className="text-base font-semibold">{data.overdueCount}</span>
          <span className="text-xs">Trễ hạn</span>
        </Link>
        <Link
          to="/tasks?tab=today"
          className="flex min-h-11 flex-col items-center justify-center rounded-md border px-2 py-1.5 transition-colors hover:bg-muted/50"
        >
          <span className="text-base font-semibold">{data.todayCount}</span>
          <span className="text-xs text-muted-foreground">Hôm nay</span>
        </Link>
        <Link
          to="/tasks?tab=upcoming"
          className="flex min-h-11 flex-col items-center justify-center rounded-md border px-2 py-1.5 transition-colors hover:bg-muted/50"
        >
          <span className="text-base font-semibold">{data.upcomingCount}</span>
          <span className="text-xs text-muted-foreground">Sắp tới</span>
        </Link>
      </div>

      {prioritized.length === 0 ? (
        <p className="text-sm text-muted-foreground">
          Không có công việc nào cần làm trong {data.upcomingDays} ngày tới.
        </p>
      ) : (
        <ul className="divide-y">
          {prioritized.map((item) => (
            <li key={item.id}>
              <Link
                to={`/tasks/${item.id}`}
                className="flex min-h-11 flex-wrap items-center justify-between gap-2 py-2.5 text-sm hover:underline"
              >
                <div className="flex flex-col">
                  <span className="font-medium">{formatFarmTaskTitle(item)}</span>
                  <span className="text-xs text-muted-foreground">
                    {item.plotCode} · {item.cropName} · <DateText value={item.scheduledDate} />
                  </span>
                </div>
                <div className="flex items-center gap-1.5">
                  <StatusBadge kind="FarmTaskStatus" value={item.status} />
                  {item.isOverdue && <StatusBadge kind="Overdue" />}
                </div>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
