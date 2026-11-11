import { AlertTriangleIcon, Edit2Icon, Loader2Icon, PlusIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";
import { useParams } from "react-router";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
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
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { SupplySelect } from "@/features/supplies/components/SupplySelect";
import {
  useCreateSeasonSupplyPlan,
  useDeleteSeasonSupplyPlan,
  useSeasonDetail,
  useSeasonSupplyPlans,
  useUpdateSeasonSupplyPlan,
} from "@/features/seasons/queries";
import type { SeasonSupplyPlanResponse } from "@/features/seasons/types";
import { formatQuantity } from "@/lib/format";
import { unitLabels } from "@/lib/enum-labels";

export function SeasonSupplyPlansPage() {
  const { id = "" } = useParams();
  const { data: season, isLoading: isSeasonLoading } = useSeasonDetail(id);
  const { data: plans, isLoading: isPlansLoading, error } = useSeasonSupplyPlans(id);

  const createMutation = useCreateSeasonSupplyPlan(id);
  const updateMutation = useUpdateSeasonSupplyPlan(id);
  const deleteMutation = useDeleteSeasonSupplyPlan(id);

  const isPlanned = season?.status === "Planned";

  // Modal thêm vật tư
  const [addModalOpen, setAddModalOpen] = useState(false);
  const [selectedSupplyId, setSelectedSupplyId] = useState("");
  const [plannedQuantity, setPlannedQuantity] = useState("");
  const [addError, setAddError] = useState<unknown>(null);

  // Modal sửa vật tư
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editingPlan, setEditingPlan] = useState<SeasonSupplyPlanResponse | null>(null);
  const [editQuantity, setEditQuantity] = useState("");
  const [editError, setEditError] = useState<unknown>(null);

  // Modal xác nhận xóa
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [deletingPlan, setDeletingPlan] = useState<SeasonSupplyPlanResponse | null>(null);

  const handleOpenAdd = () => {
    setSelectedSupplyId("");
    setPlannedQuantity("");
    setAddError(null);
    setAddModalOpen(true);
  };

  const handleAddSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!selectedSupplyId || !plannedQuantity) return;

    const qty = Number(plannedQuantity);
    if (isNaN(qty) || qty <= 0) {
      showError("Lượng dự kiến phải lớn hơn 0.");
      return;
    }

    try {
      setAddError(null);
      await createMutation.mutateAsync({
        seasonId: id,
        supplyId: selectedSupplyId,
        plannedQuantity: qty,
      });
      showSuccess("Đã thêm vật tư dự kiến vào mùa vụ.");
      setAddModalOpen(false);
    } catch (err) {
      setAddError(err);
    }
  };

  const handleOpenEdit = (plan: SeasonSupplyPlanResponse) => {
    setEditingPlan(plan);
    setEditQuantity(String(plan.plannedQuantity));
    setEditError(null);
    setEditModalOpen(true);
  };

  const handleEditSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!editingPlan || !editQuantity) return;

    const qty = Number(editQuantity);
    if (isNaN(qty) || qty <= 0) {
      showError("Lượng dự kiến phải lớn hơn 0.");
      return;
    }

    try {
      setEditError(null);
      await updateMutation.mutateAsync({
        id: editingPlan.id,
        request: {
          plannedQuantity: qty,
          version: editingPlan.version,
        },
      });
      showSuccess("Đã cập nhật lượng vật tư dự kiến.");
      setEditModalOpen(false);
    } catch (err) {
      setEditError(err);
    }
  };

  const handleOpenDelete = (plan: SeasonSupplyPlanResponse) => {
    setDeletingPlan(plan);
    setDeleteModalOpen(true);
  };

  const handleDeleteConfirm = async () => {
    if (!deletingPlan) return;
    try {
      await deleteMutation.mutateAsync(deletingPlan.id);
      showSuccess(`Đã xóa vật tư '${deletingPlan.supplyName}' khỏi danh sách dự kiến.`);
      setDeleteModalOpen(false);
    } catch (err) {
      showError(err);
    }
  };

  if (isSeasonLoading || isPlansLoading) {
    return <LoadingState variant="detail" />;
  }

  return (
    <div className="space-y-6">
      <PageHeader
        title="Khai báo vật tư dự kiến"
        description={season ? `${season.cropName} • Thửa ${season.plotCode}` : undefined}
        backTo={`/seasons/${id}`}
        action={
          isPlanned ? (
            <Button onClick={handleOpenAdd}>
              <PlusIcon className="mr-1.5 h-4 w-4" />
              Thêm vật tư
            </Button>
          ) : undefined
        }
      />

      {error ? <ErrorAlert error={error} /> : null}

      {!isPlanned && (
        <div className="rounded-lg border border-amber-200 bg-amber-50 p-3 text-sm text-amber-800">
          Mùa vụ không ở trạng thái &quot;Lên kế hoạch&quot;. Danh sách vật tư dự kiến chỉ được xem, không thể chỉnh sửa.
        </div>
      )}

      {plans && plans.length === 0 ? (
        <EmptyState
          message="Chưa có vật tư dự kiến nào được khai báo cho mùa vụ này."
          action={
            isPlanned ? (
              <Button onClick={handleOpenAdd}>
                <PlusIcon className="mr-1.5 h-4 w-4" />
                Khai báo vật tư ngay
              </Button>
            ) : undefined
          }
        />
      ) : (
        <div className="overflow-hidden rounded-lg border bg-card shadow-sm">
          <div className="divide-y">
            {plans?.map((plan) => (
              <div
                key={plan.id}
                className="flex flex-col gap-3 p-4 sm:flex-row sm:items-center sm:justify-between"
              >
                <div className="space-y-1">
                  <div className="flex flex-wrap items-center gap-2">
                    <span className="font-medium text-foreground">{plan.supplyName}</span>
                    {plan.warningCode === "SUPPLY_SHORTAGE" && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-red-100 px-2.5 py-0.5 text-xs font-semibold text-red-800">
                        <AlertTriangleIcon className="h-3 w-3" />
                        Thiếu vật tư
                      </span>
                    )}
                    {plan.warningCode === "LOW_STOCK" && (
                      <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
                        <AlertTriangleIcon className="h-3 w-3" />
                        Dưới ngưỡng
                      </span>
                    )}
                  </div>

                  <div className="flex flex-wrap gap-x-4 gap-y-1 text-sm text-muted-foreground">
                    <div>
                      <span>Dự kiến: </span>
                      <span className="font-semibold text-foreground">
                        {formatQuantity(plan.plannedQuantity, plan.unit)}
                      </span>
                    </div>
                    <div>
                      <span>Tồn hiện tại: </span>
                      <span className="font-medium text-foreground">
                        {formatQuantity(plan.currentStock, plan.unit)}
                      </span>
                    </div>
                    <div>
                      <span>Ngưỡng tối thiểu: </span>
                      <span>{formatQuantity(plan.minStockLevel, plan.unit)}</span>
                    </div>
                  </div>
                </div>

                {isPlanned && (
                  <div className="flex items-center gap-2 self-end sm:self-center">
                    <Button
                      variant="outline"
                      size="sm"
                      onClick={() => handleOpenEdit(plan)}
                      aria-label={`Sửa lượng vật tư ${plan.supplyName}`}
                    >
                      <Edit2Icon className="mr-1 h-3.5 w-3.5" />
                      Sửa
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      className="text-destructive hover:bg-destructive/10 hover:text-destructive"
                      onClick={() => handleOpenDelete(plan)}
                      aria-label={`Xóa vật tư ${plan.supplyName}`}
                    >
                      <Trash2Icon className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                )}
              </div>
            ))}
          </div>
        </div>
      )}

      {/* Modal Thêm vật tư dự kiến */}
      <Dialog open={addModalOpen} onOpenChange={setAddModalOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Thêm vật tư dự kiến</DialogTitle>
            <DialogDescription>
              Khai báo lượng vật tư dự kiến cần dùng cho mùa vụ này.
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleAddSubmit} className="space-y-4">
            {addError ? <ErrorAlert error={addError} /> : null}

            <div className="space-y-2">
              <Label htmlFor="supply-select">Vật tư *</Label>
              <SupplySelect
                id="supply-select"
                value={selectedSupplyId}
                onChange={setSelectedSupplyId}
                status="Active"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="planned-quantity">Lượng dự kiến *</Label>
              <Input
                id="planned-quantity"
                type="number"
                step="0.001"
                min="0.001"
                placeholder="Ví dụ: 50"
                value={plannedQuantity}
                onChange={(e) => setPlannedQuantity(e.target.value)}
                required
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setAddModalOpen(false)}>
                Hủy
              </Button>
              <Button type="submit" disabled={createMutation.isPending || !selectedSupplyId}>
                {createMutation.isPending ? (
                  <>
                    <Loader2Icon className="mr-1.5 h-4 w-4 animate-spin" />
                    Đang lưu...
                  </>
                ) : (
                  "Lưu vật tư"
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Modal Sửa vật tư dự kiến */}
      <Dialog open={editModalOpen} onOpenChange={setEditModalOpen}>
        <DialogContent className="sm:max-w-md">
          <DialogHeader>
            <DialogTitle>Điều chỉnh lượng dự kiến</DialogTitle>
            <DialogDescription>
              Vật tư: <strong className="text-foreground">{editingPlan?.supplyName}</strong> ({editingPlan ? unitLabels[editingPlan.unit] : ""})
            </DialogDescription>
          </DialogHeader>

          <form onSubmit={handleEditSubmit} className="space-y-4">
            {editError ? <ErrorAlert error={editError} /> : null}

            <div className="space-y-2">
              <Label htmlFor="edit-planned-quantity">Lượng dự kiến mới *</Label>
              <Input
                id="edit-planned-quantity"
                type="number"
                step="0.001"
                min="0.001"
                value={editQuantity}
                onChange={(e) => setEditQuantity(e.target.value)}
                required
              />
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditModalOpen(false)}>
                Hủy
              </Button>
              <Button type="submit" disabled={updateMutation.isPending}>
                {updateMutation.isPending ? (
                  <>
                    <Loader2Icon className="mr-1.5 h-4 w-4 animate-spin" />
                    Đang lưu...
                  </>
                ) : (
                  "Cập nhật"
                )}
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* Xác nhận xóa */}
      <ConfirmDialog
        open={deleteModalOpen}
        onOpenChange={setDeleteModalOpen}
        title="Xóa vật tư dự kiến"
        description={`Bạn có chắc muốn xóa vật tư '${deletingPlan?.supplyName}' khỏi danh sách dự kiến của mùa vụ này không?`}
        confirmLabel="Xóa vật tư"
        destructive
        onConfirm={handleDeleteConfirm}
        pending={deleteMutation.isPending}
      />
    </div>
  );
}
