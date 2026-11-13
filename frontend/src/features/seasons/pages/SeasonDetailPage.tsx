import {
  CalendarIcon,
  CheckCircle2Icon,
  ChevronRightIcon,
  FileTextIcon,
  MoreVerticalIcon,
  PlayIcon,
  PlusIcon,
  RotateCcwIcon,
  SparklesIcon,
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
import { ReasonDialog } from "@/components/common/ReasonDialog";
import { StatusBadge } from "@/components/common/StatusBadge";
import { showError, showSuccess } from "@/components/common/toast";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { SeasonNotesTab } from "@/features/seasons/components/SeasonNotesTab";
import {
  useCancelSeason,
  useCloseSeason,
  useReopenSeason,
  useSeasonDetail,
  useStartSeason,
} from "@/features/seasons/queries";
import { ApiError, type ConfirmationItem, getConfirmationItems } from "@/lib/api-error";
import { useCurrentUser } from "@/lib/auth/use-current-user";

type TabKey = "overview" | "tasks" | "harvests" | "finance" | "notes";

export function SeasonDetailPage() {
  const { id = "" } = useParams();
  const navigate = useNavigate();
  const { data: user } = useCurrentUser();
  const isOwner = user?.role === "Owner";
  const { data: season, isLoading, error } = useSeasonDetail(id);
  const startMutation = useStartSeason(id);
  const closeMutation = useCloseSeason(id);
  const cancelMutation = useCancelSeason(id);
  const reopenMutation = useReopenSeason(id);

  const [activeTab, setActiveTab] = useState<TabKey>("overview");
  const [starting, setStarting] = useState(false);
  const [showStartConfirm, setShowStartConfirm] = useState(false);

  // Trạng thái chốt mùa vụ (FR-SEA-09, BR-SEA-05, BR-GEN-14)
  const [closing, setClosing] = useState(false);
  const [confirmCloseOpen, setConfirmCloseOpen] = useState(false);
  const [closeConfirmationItems, setCloseConfirmationItems] = useState<readonly ConfirmationItem[]>([]);
  const [closeConfirmationMessage, setCloseConfirmationMessage] = useState("");
  const [produceRemainingMessage, setProduceRemainingMessage] = useState<string | null>(null);

  // Trạng thái hủy mùa vụ (FR-SEA-10, BR-SEA-06, BR-GEN-06, BR-GEN-14)
  const [cancelling, setCancelling] = useState(false);
  const [cancelReasonDialogOpen, setCancelReasonDialogOpen] = useState(false);
  const [savedCancelReason, setSavedCancelReason] = useState("");
  const [cancelError, setCancelError] = useState<unknown>(null);
  const [confirmCancelOpen, setConfirmCancelOpen] = useState(false);
  const [cancelConfirmationItems, setCancelConfirmationItems] = useState<readonly ConfirmationItem[]>([]);
  const [cancelConfirmationMessage, setCancelConfirmationMessage] = useState("");

  // Trạng thái mở lại mùa vụ (FR-SEA-14, BR-SEA-08, BR-GEN-06)
  const [reopening, setReopening] = useState(false);
  const [reopenDialogOpen, setReopenDialogOpen] = useState(false);
  const [reopenError, setReopenError] = useState<unknown>(null);

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
      showSuccess("Mùa vụ đã bắt đầu canh tác");
    } catch (err) {
      showError(err);
    } finally {
      setStarting(false);
    }
  };

  const handleClose = async (confirmed?: boolean) => {
    setClosing(true);
    try {
      await closeMutation.mutateAsync({ confirmed });
      setConfirmCloseOpen(false);
      showSuccess("Đã chốt mùa vụ");
    } catch (err) {
      if (err instanceof ApiError && err.code === "CONFIRMATION_REQUIRED") {
        setCloseConfirmationItems(getConfirmationItems(err));
        setCloseConfirmationMessage(err.message);
        setConfirmCloseOpen(true);
      } else if (err instanceof ApiError && err.code === "SEASON_PRODUCE_REMAINING") {
        setProduceRemainingMessage(err.message);
      } else {
        showError(err);
      }
    } finally {
      setClosing(false);
    }
  };

  const handleCancel = async (reason: string, confirmed?: boolean) => {
    setCancelling(true);
    setCancelError(null);
    try {
      await cancelMutation.mutateAsync({ reason, confirmed });
      setCancelReasonDialogOpen(false);
      setConfirmCancelOpen(false);
      showSuccess("Đã hủy mùa vụ");
    } catch (err) {
      if (err instanceof ApiError && err.code === "CONFIRMATION_REQUIRED") {
        setSavedCancelReason(reason);
        setCancelConfirmationItems(getConfirmationItems(err));
        setCancelConfirmationMessage(err.message);
        setCancelReasonDialogOpen(false);
        setConfirmCancelOpen(true);
      } else {
        // Chỉ truyền lỗi vào ReasonDialog qua state cancelError, không gọi showError toast để tránh trùng lặp
        setCancelError(err);
      }
    } finally {
      setCancelling(false);
    }
  };

  const handleReopen = async (reason: string) => {
    setReopening(true);
    setReopenError(null);
    try {
      await reopenMutation.mutateAsync({ reason });
      setReopenDialogOpen(false);
      showSuccess("Đã mở lại mùa vụ. Sửa xong thì chốt lại mùa vụ.");
    } catch (err) {
      // Chỉ truyền lỗi vào ReasonDialog qua state reopenError, không gọi showError toast để tránh trùng lặp
      setReopenError(err);
    } finally {
      setReopening(false);
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
              {!season.isOutOfScope && (
                <DropdownMenuItem onClick={() => navigate(`/seasons/${id}/supply-plans`)}>
                  Vật tư dự kiến
                </DropdownMenuItem>
              )}
              {season.canReopen && (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    onClick={() => {
                      setReopenError(null);
                      setReopenDialogOpen(true);
                    }}
                  >
                    Mở lại mùa vụ
                  </DropdownMenuItem>
                </>
              )}
              {season.canCancel && (
                <>
                  <DropdownMenuSeparator />
                  <DropdownMenuItem
                    className="text-destructive focus:text-destructive"
                    onClick={() => {
                      setCancelError(null);
                      setCancelReasonDialogOpen(true);
                    }}
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

        {/* Thông báo lý do hủy nếu mùa vụ đã hủy: dùng nền trung tính */}
        {season.status === "Cancelled" && season.cancelReason && (
          <div className="rounded-md border border-muted-foreground/20 bg-muted/50 p-3 text-xs text-foreground">
            <span className="font-semibold text-muted-foreground">Lý do hủy: </span>
            {season.cancelReason}
          </div>
        )}

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

        {/* Nút hành động chính theo trạng thái */}
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
        {season.canClose && (
          <Button
            className="w-full bg-emerald-600 hover:bg-emerald-700 text-white"
            onClick={() => handleClose(false)}
            disabled={closing}
          >
            <CheckCircle2Icon className="mr-2 h-4 w-4" />
            {closing ? "Đang chốt mùa vụ..." : "Chốt mùa vụ"}
          </Button>
        )}
        {season.canReopen && (
          <Button
            variant="outline"
            className="w-full"
            onClick={() => {
              setReopenError(null);
              setReopenDialogOpen(true);
            }}
            disabled={reopening}
          >
            <RotateCcwIcon className="mr-2 h-4 w-4" />
            Mở lại mùa vụ
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
          {/* Placeholder tóm tắt mùa vụ bằng AI khi mùa vụ đã chốt (FR-AI-01) */}
          {season.status === "Closed" && (
            <div className="rounded-lg border border-dashed bg-card p-4 space-y-2">
              <div className="flex items-center gap-2 text-sm font-semibold text-foreground">
                <SparklesIcon className="h-4 w-4 text-amber-500" />
                <span>Tóm tắt mùa vụ bằng AI</span>
              </div>
              <p className="text-xs text-muted-foreground">
                Tính năng phân tích và tổng kết kinh nghiệm mùa vụ bằng AI đang được phát triển.
              </p>
            </div>
          )}

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

      {/* Xác nhận bắt đầu mùa vụ */}
      <ConfirmDialog
        open={showStartConfirm}
        onOpenChange={setShowStartConfirm}
        title="Bắt đầu mùa vụ?"
        description="Mùa vụ sẽ chuyển sang trạng thái Đang canh tác. Bạn sẽ không thể quay lại trạng thái Lên kế hoạch."
        confirmLabel="Bắt đầu mùa vụ"
        onConfirm={() => void handleStart()}
        pending={starting}
      />

      {/* Xác nhận chốt mùa vụ khi còn việc chưa làm (FR-SEA-09, BR-GEN-14) */}
      <ConfirmDialog
        open={confirmCloseOpen}
        onOpenChange={setConfirmCloseOpen}
        title="Xác nhận chốt mùa vụ"
        description={
          closeConfirmationMessage ||
          "Mùa vụ còn công việc chưa hoàn thành. Các công việc này sẽ được chuyển sang trạng thái Đã hủy."
        }
        items={closeConfirmationItems}
        confirmLabel="Chốt mùa vụ"
        onConfirm={() => void handleClose(true)}
        pending={closing}
        destructive
      />

      {/* Hộp thoại thông báo khi không thể chốt do còn tồn nông sản */}
      <Dialog
        open={Boolean(produceRemainingMessage)}
        onOpenChange={(open) => !open && setProduceRemainingMessage(null)}
      >
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Không thể chốt mùa vụ</DialogTitle>
            <DialogDescription>{produceRemainingMessage}</DialogDescription>
          </DialogHeader>
          <DialogFooter className="flex flex-col-reverse sm:flex-row sm:justify-end gap-2">
            <Button variant="outline" onClick={() => setProduceRemainingMessage(null)}>
              Đóng
            </Button>
            <Button asChild onClick={() => setProduceRemainingMessage(null)}>
              <Link to={`/produce-stock?seasonId=${id}`}>Xem tồn nông sản</Link>
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Hộp thoại nhập lý do hủy mùa vụ (FR-SEA-10, BR-GEN-06) */}
      <ReasonDialog
        open={cancelReasonDialogOpen}
        onOpenChange={setCancelReasonDialogOpen}
        title="Hủy mùa vụ"
        description="Vật tư đã xuất vẫn tính là chi phí của mùa vụ. Muốn trả vật tư chưa dùng về kho thì hủy giao dịch xuất trước."
        confirmLabel="Hủy mùa vụ"
        onConfirm={(reason) => void handleCancel(reason, false)}
        pending={cancelling}
        error={cancelError}
        destructive
      />

      {/* Xác nhận hủy mùa vụ khi còn việc chưa làm (FR-SEA-10, BR-GEN-14) */}
      <ConfirmDialog
        open={confirmCancelOpen}
        onOpenChange={setConfirmCancelOpen}
        title="Xác nhận hủy mùa vụ"
        description={
          cancelConfirmationMessage ||
          "Mùa vụ còn công việc chưa hoàn thành. Các công việc này sẽ được chuyển sang trạng thái Đã hủy."
        }
        items={cancelConfirmationItems}
        confirmLabel="Hủy mùa vụ"
        onConfirm={() => void handleCancel(savedCancelReason, true)}
        pending={cancelling}
        destructive
      />

      {/* Hộp thoại nhập lý do mở lại mùa vụ (FR-SEA-14, BR-GEN-06) */}
      <ReasonDialog
        open={reopenDialogOpen}
        onOpenChange={setReopenDialogOpen}
        title="Mở lại mùa vụ"
        description="Mở lại mùa vụ đã chốt về trạng thái Đang canh tác để điều chỉnh dữ liệu. Bắt buộc nhập lý do."
        confirmLabel="Mở lại mùa vụ"
        onConfirm={(reason) => void handleReopen(reason)}
        pending={reopening}
        error={reopenError}
      />
    </div>
  );
}
