import { useQueries } from "@tanstack/react-query";
import { CircleCheckIcon, CircleIcon } from "lucide-react";
import type { ComponentType, ReactNode } from "react";
import { Link, Navigate } from "react-router";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { buttonVariants } from "@/components/ui/button";
import { TodayTasksCard } from "@/features/farm-tasks/components/TodayTasksCard";
import { DebtsCard } from "@/features/sales/components/DebtsCard";
import { ActiveSeasonsCard } from "@/features/seasons/components/ActiveSeasonsCard";
import { LowStockCard } from "@/features/supplies/components/LowStockCard";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";
import { ACCESS, type Access, canAccess } from "@/lib/auth/access";
import { homePathFor } from "@/lib/auth/home-path";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { formatWeekdayDate } from "@/lib/format";
import { cn } from "@/lib/utils";

interface TodayBlock {
  key: string;
  access: Access;
  /** Component do người phụ trách module viết trong thư mục module; chưa có thì để `null`. */
  component: ComponentType | null;
}

// Thứ tự cố định của trang "Hôm nay". Khối đọc dữ liệu trong phạm vi làm việc thì tự lọc theo phạm vi,
// tự hiện đang tải và lỗi của mình nên một khối lỗi không làm hỏng các khối khác.
const blocks: readonly TodayBlock[] = [
  { key: "tasks", access: ACCESS.household, component: TodayTasksCard },
  { key: "active-seasons", access: ACCESS.household, component: ActiveSeasonsCard },
  { key: "low-stock", access: ACCESS.household, component: LowStockCard },
  { key: "debts", access: ACCESS.financeManager, component: DebtsCard },
  // Hỏi trợ lý: AskAssistantCard của module AI, cắm khi FR-AI-02 vào develop.
  { key: "ask-assistant", access: ACCESS.household, component: null },
];

/** Trang "Hôm nay" (`/`) của Chủ hộ, Thành viên hộ. Nhân công, Quản trị mở `/` thì chuyển tới trang đầu của mình. */
export function TodayPage() {
  const { data: user } = useCurrentUser();
  if (!user) {
    return null;
  }

  if (user.role !== "Owner" && user.role !== "Member") {
    return <Navigate to={homePathFor(user.role)} replace />;
  }

  const visibleBlocks = blocks.filter((block) => block.component !== null && canAccess(user, block.access));
  const blockList = visibleBlocks.map(({ key, component: Block }) => Block && <Block key={key} />);

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={user.household?.name ?? "Hôm nay"} description={formatWeekdayDate(new Date())} />
      {/* Chỉ Chủ hộ tạo được thửa, cây, mùa vụ nên danh sách "Bắt đầu" chỉ hiện với Chủ hộ. */}
      {user.role === "Owner" ? <OwnerContent blocks={blockList} /> : blockList}
    </div>
  );
}

// Chỉ cần biết hộ đã có bản ghi nào chưa: lấy một dòng, đọc tổng số ở meta.
async function getCount(path: string): Promise<number> {
  const response = await apiClient.get<ResponseEnvelope<unknown[]>>(path, { params: { pageSize: 1 } });
  return response.data.meta?.totalCount ?? 0;
}

// Mỗi khóa nằm dưới khóa gốc của module ("plots", "crops", "seasons") nên tạo bản ghi xong thì tự làm mới.
const steps = [
  { key: "plots", name: "Thửa đất", action: "Tạo thửa đất", to: "/plots/new" },
  { key: "crops", name: "Cây trồng", action: "Tạo cây trồng", to: "/crops/new" },
  { key: "seasons", name: "Mùa vụ", action: "Tạo mùa vụ", to: "/seasons/new" },
] as const;

/**
 * Hộ mới chưa đủ thửa đất, cây trồng, mùa vụ thì danh sách "Bắt đầu" thay cho các khối;
 * đủ cả ba bước thì chỉ hiện các khối.
 */
function OwnerContent({ blocks: blockList }: { blocks: ReactNode }) {
  const counts = useQueries({
    queries: steps.map((step) => ({
      queryKey: [step.key, "count"],
      queryFn: () => getCount(`/${step.key}`),
    })),
  });

  const header = (
    <div>
      <h2 className="font-semibold">Bắt đầu</h2>
      <p className="text-sm text-muted-foreground">Ba bước để bắt đầu ghi chép mùa vụ đầu tiên.</p>
    </div>
  );

  // Chưa biết hộ đã có dữ liệu hay chưa thì không hiện nút "Tạo ...": hộ đã có sẽ thấy nút sai trong lúc chờ.
  if (counts.some((count) => count.isPending)) {
    return (
      <section className="flex flex-col gap-3 rounded-lg border p-4">
        {header}
        <LoadingState rows={3} />
      </section>
    );
  }

  // Không biết hộ mới hay cũ thì vẫn hiện các khối để Chủ hộ dùng được trang.
  const failed = counts.find((count) => count.isError);
  if (failed) {
    return (
      <>
        <section className="flex flex-col gap-3 rounded-lg border p-4">
          {header}
          <ErrorAlert
            error={failed.error}
            onRetry={() => {
              for (const count of counts) {
                if (count.isError) void count.refetch();
              }
            }}
          />
        </section>
        {blockList}
      </>
    );
  }

  const done = counts.map((count) => (count.data ?? 0) > 0);
  if (done.every(Boolean)) {
    return blockList;
  }

  return (
    <section className="flex flex-col gap-3 rounded-lg border p-4">
      {header}
      <ol className="flex flex-col gap-2">
        {steps.map((step, index) => (
          <li key={step.to} className="flex min-h-11 items-center gap-3">
            {done[index] ? (
              <CircleCheckIcon className="size-5 shrink-0 text-primary" aria-label="Đã xong" />
            ) : (
              <CircleIcon className="size-5 shrink-0 text-muted-foreground" aria-label="Chưa làm" />
            )}
            <span className={cn("flex-1", done[index] && "text-muted-foreground line-through")}>
              {index + 1}. {step.name}
            </span>
            {!done[index] && (
              <Link to={step.to} className={buttonVariants({ size: "sm", className: "h-11" })}>
                {step.action}
              </Link>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
