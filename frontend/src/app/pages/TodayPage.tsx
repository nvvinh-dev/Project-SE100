import { useQuery } from "@tanstack/react-query";
import { CircleCheckIcon, CircleIcon } from "lucide-react";
import type { ComponentType } from "react";
import { Link, Navigate } from "react-router";
import { PageHeader } from "@/components/common/PageHeader";
import { buttonVariants } from "@/components/ui/button";
import { apiClient, type ResponseEnvelope } from "@/lib/api-client";
import { ACCESS, type Access, canAccess } from "@/lib/auth/access";
import { homePathFor } from "@/lib/auth/home-path";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { formatWeekdayDate } from "@/lib/format";
import { cn } from "@/lib/utils";

interface TodayBlock {
  key: string;
  access: Access;
  /** Component do người phụ trách module viết trong thư mục module; chưa có thì để `null`, ghép ở CHUNG-02. */
  component: ComponentType | null;
}

// Thứ tự cố định của trang "Hôm nay". Khối đọc dữ liệu trong phạm vi làm việc thì tự lọc theo phạm vi.
const blocks: readonly TodayBlock[] = [
  // Việc cần làm: TodayTasksCard (Hưng).
  { key: "tasks", access: ACCESS.household, component: null },
  // Mùa vụ đang canh tác: ActiveSeasonsCard (Hưng).
  { key: "active-seasons", access: ACCESS.household, component: null },
  // Vật tư dưới ngưỡng: LowStockCard (Giang).
  { key: "low-stock", access: ACCESS.household, component: null },
  // Công nợ chưa thu: DebtsCard (Giang).
  { key: "debts", access: ACCESS.financeManager, component: null },
  // Hỏi trợ lý: AskAssistantCard (Giang).
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

  return (
    <div className="flex flex-col gap-6">
      <PageHeader title={user.household?.name ?? "Hôm nay"} description={formatWeekdayDate(new Date())} />
      {/* Chỉ Chủ hộ tạo được thửa, cây, mùa vụ nên danh sách "Bắt đầu" chỉ hiện với Chủ hộ. */}
      {user.role === "Owner" && <GettingStarted />}
      {blocks
        .filter((block) => block.component !== null && canAccess(user, block.access))
        .map(({ key, component: Block }) => Block && <Block key={key} />)}
    </div>
  );
}

// Chỉ cần biết hộ đã có thửa nào chưa: lấy một dòng, đọc tổng số ở meta.
async function getPlotCount(): Promise<number> {
  const response = await apiClient.get<ResponseEnvelope<unknown[]>>("/plots", { params: { pageSize: 1 } });
  return response.data.meta?.totalCount ?? 0;
}

/**
 * Danh sách "Bắt đầu" của hộ mới: tạo thửa đất → tạo cây trồng → tạo mùa vụ. Hiện tới khi đủ ba bước.
 * Bước cây trồng, mùa vụ chưa có API để kiểm tra nên luôn hiện là chưa xong; nối ở CHUNG-02.
 */
function GettingStarted() {
  const plotCount = useQuery({ queryKey: ["plots", "count"], queryFn: getPlotCount });

  const steps = [
    { name: "Thửa đất", action: "Tạo thửa đất", to: "/plots/new", done: (plotCount.data ?? 0) > 0 },
    { name: "Cây trồng", action: "Tạo cây trồng", to: "/crops/new", done: false },
    { name: "Mùa vụ", action: "Tạo mùa vụ", to: "/seasons/new", done: false },
  ];

  if (steps.every((step) => step.done)) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3 rounded-lg border p-4">
      <div>
        <h2 className="font-semibold">Bắt đầu</h2>
        <p className="text-sm text-muted-foreground">Ba bước để bắt đầu ghi chép mùa vụ đầu tiên.</p>
      </div>
      <ol className="flex flex-col gap-2">
        {steps.map((step, index) => (
          <li key={step.to} className="flex min-h-11 items-center gap-3">
            {step.done ? (
              <CircleCheckIcon className="size-5 shrink-0 text-primary" aria-label="Đã xong" />
            ) : (
              <CircleIcon className="size-5 shrink-0 text-muted-foreground" aria-label="Chưa làm" />
            )}
            <span className={cn("flex-1", step.done && "text-muted-foreground line-through")}>
              {index + 1}. {step.name}
            </span>
            {!step.done && (
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
