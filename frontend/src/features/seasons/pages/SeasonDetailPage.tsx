import {
  CalendarIcon,
  CheckCircle2Icon,
  ChevronRightIcon,
  FileTextIcon,
  MoreVerticalIcon,
  PlayIcon,
  PlusIcon,
  SproutIcon,
} from "lucide-react";
import { useState } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DateText } from "@/components/common/DateText";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { QuantityText } from "@/components/common/QuantityText";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showError, showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SeasonNotesTab } from "@/features/seasons/components/SeasonNotesTab";
import { useSeasonDetail, useStartSeason } from "@/features/seasons/queries";
import { useCurrentUser } from "@/lib/auth/use-current-user";

type TabKey = "overview" | "tasks" | "harvests" | "finance" | "notes";

export function SeasonDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: user } = useCurrentUser();
  const isOwner = user?.role === "Owner";
  const { data: season, isLoading, error } = useSeasonDetail(id);
  const startMutation = useStartSeason(id);

  const [activeTab, setActiveTab] = useState<TabKey>("overview");
  const [starting, setStarting] = useState(false);
  const [showStartConfirm, setShowStartConfirm] = useState(false);

  if (isLoading) {
    return <LoadingState variant="detail" />;
  }

  if (error || !season) {
    return (
      <div className="space-y-4 p-4">
        <PageHeader title="Chi tiết mùa vụ" backTo="/seasons" />
        <ErrorAlert error={error ?? new Error("Không tìm thấy mùa vụ")} />
      </div>
    );
  }

  const handleStart = async () => {
    setShowStartConfirm(false);
    setStarting(true);
    try {
      await startMutation.mutateAsync();
      showSuccess("Mùa vụ đã bắt đầu canh tác!");
    } catch (err) {
      showError(err);
    } finally {
      setStarting(false);
    }
  };

  const progressPercent =
    season.targetYield > 0
      ? Math.min(100, Math.round((season.harvestedYield / season.targetYield) * 100))
      : 0;

  return (
    <div className="space-y-4 pb-12">
      {/* Header */}
      <PageHeader
        title={`${season.cropName} · ${season.plotCode}`}
        backTo="/seasons"
        action={
          <DropdownMenu>
            <DropdownMenuTrigger asChild>
              <Button variant="ghost" size="icon">
                <MoreVerticalIcon className="h-5 w-5" />
                <span className="sr-only">Thao tác</span>
              </Button>
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">
              {season.canEdit && (
                <DropdownMenuItem onClick={() => navigate(`/seasons/${id}/edit`)}>
                  Sửa mùa vụ
                </DropdownMenuItem>
              )}
              {!season.isOutOfScope && isOwner && (
                <DropdownMenuItem onClick={() => navigate(`/seasons/${id}/members`)}>
                  Phân công thành viên
                </DropdownMenuItem>
              )}
              {season.canCancel && (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    className="text-destructive"
                    onClick={() => navigate(`/seasons/${id}/cancel`)}
                  >
                    Hủy mùa vụ
                  </DropdownMenuItem>
                </>
              )}
            </DropdownMenuContent>
          </DropdownMenu>
        }
      />

      {/* Thẻ tóm tắt thông tin mùa vụ */}
      <div className="rounded-lg border bg-card p-4 space-y-4">
        <div className="flex items-center justify-between">
          <StatusBadge kind="SeasonStatus" value={season.status} />
          <div className="flex items-center text-xs text-muted-foreground gap-1">
            <CalendarIcon className="h-3.5 w-3.5" />
            <DateText value={season.startDate} /> → <DateText value={season.expectedHarvestDate} />
          </div>
        </div>

        {/* Sản lượng & tiến độ */}
        <div className="space-y-1.5">
          <div className="flex justify-between text-xs font-medium">
            <span>
              Mục tiêu: <QuantityText value={season.targetYield} unit={season.yieldUnit} />
            </span>
            <span>
              Đã thu: <QuantityText value={season.harvestedYield} unit={season.yieldUnit} /> ({progressPercent}%)
            </span>
          </div>
          <div className="h-2.5 w-full overflow-hidden rounded-full bg-secondary">
            <div
              className="h-full bg-emerald-600 transition-all duration-300"
              style={{ width: `${progressPercent}%` }}
            />
          </div>
        </div>

        {/* Nút hành động chính */}
        {season.canStart && (
          <Button
            className="w-full bg-emerald-600 hover:bg-emerald-700 text-white"
            onClick={() => setShowStartConfirm(true)}
            disabled={starting}
          >
            <PlayIcon className="mr-2 h-4 w-4" />
            {starting ? "Đang xử lý..." : "Bắt đầu mùa vụ"}
          </Button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex border-b overflow-x-auto text-sm font-medium">
        <button
          onClick={() => setActiveTab("overview")}
          className={`px-4 py-2.5 whitespace-nowrap border-b-2 transition-colors ${
            activeTab === "overview"
              ? "border-primary text-primary font-semibold"
              : "border-transparent text-muted-foreground hover:text-foreground"
          }`}
        >
          Tổng quan
        </button>
        {!season.isOutOfScope && (
          <>
            <button
              onClick={() => setActiveTab("tasks")}
              className={`px-4 py-2.5 whitespace-nowrap border-b-2 transition-colors ${
                activeTab === "tasks"
                  ? "border-primary text-primary font-semibold"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              }`}
            >
              Công việc
            </button>
            <button
              onClick={() => setActiveTab("harvests")}
              className={`px-4 py-2.5 whitespace-nowrap border-b-2 transition-colors ${
                activeTab === "harvests"
                  ? "border-primary text-primary font-semibold"
                  : "border-transparent text-muted-foreground hover:text-foreground"
              }`}
            >
              Thu hoạch
            </button>
          </>
        )}
        {season.financialSummary && (
          <button
            onClick={() => setActiveTab("finance")}
            className={`px-4 py-2.5 whitespace-nowrap border-b-2 transition-colors ${
              activeTab === "finance"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Tài chính
          </button>
        )}
        {!season.isOutOfScope && (
          <button
            onClick={() => setActiveTab("notes")}
            className={`px-4 py-2.5 whitespace-nowrap border-b-2 transition-colors ${
              activeTab === "notes"
                ? "border-primary text-primary font-semibold"
                : "border-transparent text-muted-foreground hover:text-foreground"
            }`}
          >
            Ghi chú
          </button>
        )}
      </div>

      {/* Nội dung Tab */}
      {activeTab === "overview" && (
        <div className="space-y-4">
          {/* Hướng dẫn khi mùa vụ Planned */}
          {season.status === "Planned" && !season.isOutOfScope && (
            <div className="rounded-lg border bg-amber-50/50 dark:bg-amber-950/20 p-4 space-y-3">
              <h4 className="text-sm font-semibold text-amber-900 dark:text-amber-200">
                Việc cần chuẩn bị trước khi bắt đầu mùa vụ:
              </h4>
              <div className="space-y-2 text-sm">
                <Link
                  to={`/seasons/${id}/supply-plans`}
                  className="flex items-center justify-between rounded-md bg-background p-2.5 border hover:bg-muted/50"
                >
                  <span className="flex items-center gap-2">
                    <CheckCircle2Icon className="h-4 w-4 text-muted-foreground" />
                    Khai báo vật tư dự kiến
                  </span>
                  <ChevronRightIcon className="h-4 w-4 text-muted-foreground" />
                </Link>
                <Link
                  to={`/tasks/new?seasonId=${id}`}
                  className="flex items-center justify-between rounded-md bg-background p-2.5 border hover:bg-muted/50"
                >
                  <span className="flex items-center gap-2">
                    <CheckCircle2Icon className="h-4 w-4 text-muted-foreground" />
                    Lập lịch công việc ban đầu
                  </span>
                  <ChevronRightIcon className="h-4 w-4 text-muted-foreground" />
                </Link>
                <Link
                  to={`/seasons/${id}/members`}
                  className="flex items-center justify-between rounded-md bg-background p-2.5 border hover:bg-muted/50"
                >
                  <span className="flex items-center gap-2">
                    <CheckCircle2Icon className="h-4 w-4 text-muted-foreground" />
                    Phân công người vào mùa vụ
                  </span>
                  <ChevronRightIcon className="h-4 w-4 text-muted-foreground" />
                </Link>
              </div>
            </div>
          )}

          {/* Tiến độ công việc */}
          {!season.isOutOfScope && (
            <div className="rounded-lg border bg-card p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Tiến độ công việc</span>
                <span className="text-sm font-semibold">
                  {season.completedTasksCount} / {season.totalTasksCount} việc
                </span>
              </div>
              <div className="flex justify-end">
                <Button variant="link" size="sm" asChild className="p-0 h-auto">
                  <Link to={`/tasks?seasonId=${id}`}>Xem danh sách công việc ›</Link>
                </Button>
              </div>
            </div>
          )}

          {/* Tồn nông sản (BR-USR-13: chỉ hiển thị khi trong phân công hoặc có ManageFinance) */}
          {(!season.isOutOfScope || user?.canManageFinance) && (
            <div className="rounded-lg border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Tồn nông sản</span>
                <Button variant="link" size="sm" asChild className="p-0 h-auto">
                  <Link to="/produce-stock">Xem kho nông sản ›</Link>
                </Button>
              </div>
              {season.produceStocks.length === 0 ? (
                <p className="text-xs text-muted-foreground">Chưa có tồn nông sản.</p>
              ) : (
                <div className="space-y-2">
                  {season.produceStocks.map((stock) => (
                    <div
                      key={stock.grade}
                      className="flex items-center justify-between rounded-md bg-muted/40 p-2.5 text-sm"
                    >
                      <span>
                        {stock.grade === "Grade1"
                          ? "Loại 1"
                          : stock.grade === "Grade2"
                            ? "Loại 2"
                            : "Loại 3"}
                      </span>
                      <div className="flex items-center gap-3">
                        <span className="font-semibold">
                          <QuantityText value={stock.quantity} unit={stock.unit} />
                        </span>
                        {stock.quantity > 0 && (
                          <Button size="sm" variant="outline" asChild className="h-7 text-xs">
                            <Link to={`/sales/new?seasonId=${id}&grade=${stock.grade}`}>Bán</Link>
                          </Button>
                        )}
                      </div>
                    </div>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Người được phân công */}
          {!season.isOutOfScope && (
            <div className="rounded-lg border bg-card p-4 space-y-3">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Người được phân công</span>
                {isOwner && (
                  <Button variant="link" size="sm" asChild className="p-0 h-auto">
                    <Link to={`/seasons/${id}/members`}>Quản lý ›</Link>
                  </Button>
                )}
              </div>
              {season.assignments.length === 0 ? (
                <p className="text-xs text-muted-foreground">Chưa có người được phân công.</p>
              ) : (
                <div className="flex flex-wrap gap-1.5">
                  {season.assignments.map((a) => (
                    <span
                      key={a.id}
                      className="inline-flex items-center rounded-full bg-secondary px-2.5 py-0.5 text-xs font-medium"
                    >
                      {a.fullName} ({a.role === "Member" ? "Thành viên" : "Nhân công"})
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}

          {/* Tóm tắt tài chính (nếu có quyền) */}
          {season.financialSummary && (
            <div className="rounded-lg border bg-card p-4 space-y-2">
              <div className="flex items-center justify-between">
                <span className="text-sm font-medium">Lợi nhuận (tạm tính)</span>
                <span className="text-base font-bold text-emerald-600">
                  <MoneyText amount={season.financialSummary.estimatedProfit} />
                </span>
              </div>
              <div className="flex justify-between text-xs text-muted-foreground pt-1 border-t">
                <span>
                  Doanh thu: <MoneyText amount={season.financialSummary.totalRevenue} />
                </span>
                <span>
                  Chi phí: <MoneyText amount={season.financialSummary.totalExpense} />
                </span>
              </div>
            </div>
          )}
        </div>
      )}

      {/* Tab Công việc */}
      {activeTab === "tasks" && (
        <div className="space-y-4">
          <div className="flex justify-between items-center">
            <span className="text-sm font-semibold">Công việc của mùa vụ</span>
            <Button size="sm" asChild>
              <Link to={`/tasks/new?seasonId=${id}`}>
                <PlusIcon className="mr-1.5 h-4 w-4" /> Thêm công việc
              </Link>
            </Button>
          </div>
          <div className="rounded-lg border bg-card p-6 text-center space-y-2">
            <SproutIcon className="mx-auto h-8 w-8 text-muted-foreground" />
            <p className="text-sm font-medium">
              Đã hoàn thành {season.completedTasksCount} / {season.totalTasksCount} công việc
            </p>
            <Button variant="outline" asChild>
              <Link to={`/tasks?seasonId=${id}`}>Mở trang quản lý công việc</Link>
            </Button>
          </div>
        </div>
      )}

      {/* Tab Thu hoạch */}
      {activeTab === "harvests" && (
        <div className="space-y-4">
          <div className="flex justify-between items-center">
            <span className="text-sm font-semibold">Phiếu thu hoạch</span>
            <Button size="sm" asChild>
              <Link to={`/harvests/new?seasonId=${id}`}>
                <PlusIcon className="mr-1.5 h-4 w-4" /> Thu hoạch
              </Link>
            </Button>
          </div>
          <div className="rounded-lg border bg-card p-6 text-center space-y-2">
            <FileTextIcon className="mx-auto h-8 w-8 text-muted-foreground" />
            <p className="text-sm font-medium">
              Tổng sản lượng đã thu:{" "}
              <QuantityText value={season.harvestedYield} unit={season.yieldUnit} />
            </p>
            <Button variant="outline" asChild>
              <Link to={`/harvests?seasonId=${id}`}>Mở trang danh sách thu hoạch</Link>
            </Button>
          </div>
        </div>
      )}

      {/* Tab Tài chính */}
      {activeTab === "finance" && season.financialSummary && (
        <div className="space-y-4">
          <div className="rounded-lg border bg-card p-4 space-y-3">
            <h4 className="text-sm font-semibold">Báo cáo tài chính mùa vụ</h4>
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div className="rounded-md bg-muted/40 p-3">
                <span className="text-xs text-muted-foreground">Tổng doanh thu</span>
                <p className="text-base font-bold text-foreground">
                  <MoneyText amount={season.financialSummary.totalRevenue} />
                </p>
              </div>
              <div className="rounded-md bg-muted/40 p-3">
                <span className="text-xs text-muted-foreground">Tổng chi phí</span>
                <p className="text-base font-bold text-foreground">
                  <MoneyText amount={season.financialSummary.totalExpense} />
                </p>
              </div>
            </div>
            <div className="rounded-md bg-emerald-50 dark:bg-emerald-950/20 p-3 flex justify-between items-center">
              <span className="text-sm font-medium text-emerald-900 dark:text-emerald-200">
                Lợi nhuận tạm tính
              </span>
              <span className="text-lg font-extrabold text-emerald-600">
                <MoneyText amount={season.financialSummary.estimatedProfit} />
              </span>
            </div>
          </div>
        </div>
      )}

      {/* Tab Ghi chú */}
      {activeTab === "notes" && <SeasonNotesTab seasonId={id} />}

      <ConfirmDialog
        open={showStartConfirm}
        onOpenChange={setShowStartConfirm}
        title="Bắt đầu mùa vụ?"
        description="Mùa vụ sẽ chuyển sang trạng thái Đang canh tác. Bạn sẽ không thể quay lại trạng thái Lên kế hoạch."
        confirmLabel="Bắt đầu mùa vụ"
        onConfirm={() => void handleStart()}
        pending={starting}
      />
    </div>
  );
}
