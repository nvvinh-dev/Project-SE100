import { zodResolver } from "@hookform/resolvers/zod";
import { AlertTriangleIcon, Edit2Icon, Loader2Icon, PlusIcon, Trash2Icon } from "lucide-react";
import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { useParams } from "react-router";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DataList, type DataListColumn } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { showError, showSuccess, showWarnings } from "@/components/common/toast";
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
import {
  editSeasonSupplyPlanSchema,
  seasonSupplyPlanSchema,
  setEditSupplyPlanFieldErrors,
  setSupplyPlanFieldErrors,
  type EditSeasonSupplyPlanFormValues,
  type SeasonSupplyPlanFormValues,
} from "@/features/seasons/schemas";
import type { SeasonSupplyPlanResponse } from "@/features/seasons/types";
import { unitLabels } from "@/lib/enum-labels";
import { formatQuantity, parseDecimalInput } from "@/lib/format";

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
  const [addError, setAddError] = useState<unknown>(null);

  const addForm = useForm<SeasonSupplyPlanFormValues>({
    resolver: zodResolver(seasonSupplyPlanSchema()),
    defaultValues: {
      supplyId: "",
      plannedQuantity: "",
    },
  });

  // Modal sửa vật tư
  const [editModalOpen, setEditModalOpen] = useState(false);
  const [editingPlan, setEditingPlan] = useState<SeasonSupplyPlanResponse | null>(null);
  const [editError, setEditError] = useState<unknown>(null);

  const editForm = useForm<EditSeasonSupplyPlanFormValues>({
    resolver: zodResolver(editSeasonSupplyPlanSchema()),
    defaultValues: {
      plannedQuantity: "",
    },
  });

  // Modal xác nhận xóa
  const [deleteModalOpen, setDeleteModalOpen] = useState(false);
  const [deletingPlan, setDeletingPlan] = useState<SeasonSupplyPlanResponse | null>(null);

  const handleOpenAdd = () => {
    addForm.reset({
      supplyId: "",
      plannedQuantity: "",
    });
    setAddError(null);
    setAddModalOpen(true);
  };

  const handleAddSubmit = async (values: SeasonSupplyPlanFormValues) => {
    try {
      setAddError(null);
      const result = await createMutation.mutateAsync({
        seasonId: id,
        supplyId: values.supplyId,
        plannedQuantity: parseDecimalInput(values.plannedQuantity) ?? 0,
      });
      showSuccess("Đã thêm vật tư dự kiến vào mùa vụ.");
      if (result.warnings && result.warnings.length > 0) {
        showWarnings(result.warnings);
      }
      setAddModalOpen(false);
    } catch (err) {
      if (!setSupplyPlanFieldErrors(err, addForm.setError)) {
        setAddError(err);
      }
    }
  };

  const handleOpenEdit = (plan: SeasonSupplyPlanResponse) => {
    setEditingPlan(plan);
    editForm.reset({
      plannedQuantity: String(plan.plannedQuantity).replace(".", ","),
    });
    setEditError(null);
    setEditModalOpen(true);
  };

  const handleEditSubmit = async (values: EditSeasonSupplyPlanFormValues) => {
    if (!editingPlan) return;

    try {
      setEditError(null);
      const result = await updateMutation.mutateAsync({
        id: editingPlan.id,
        request: {
          plannedQuantity: parseDecimalInput(values.plannedQuantity) ?? 0,
          version: editingPlan.version,
        },
      });
      showSuccess("Đã cập nhật lượng vật tư dự kiến.");
      if (result.warnings && result.warnings.length > 0) {
        showWarnings(result.warnings);
      }
      setEditModalOpen(false);
    } catch (err) {
      if (!setEditSupplyPlanFieldErrors(err, editForm.setError)) {
        setEditError(err);
      }
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

  const renderWarningBadge = (plan: SeasonSupplyPlanResponse) => {
    if (plan.warningCode === "SUPPLY_SHORTAGE") {
      return (
        <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
          <AlertTriangleIcon className="h-3 w-3" />
          Thiếu vật tư
        </span>
      );
    }
    if (plan.warningCode === "LOW_STOCK") {
      return (
        <span className="inline-flex items-center gap-1 rounded-full bg-amber-100 px-2.5 py-0.5 text-xs font-semibold text-amber-800">
          <AlertTriangleIcon className="h-3 w-3" />
          Dưới ngưỡng
        </span>
      );
    }
    return null;
  };

  const columns: DataListColumn<SeasonSupplyPlanResponse>[] = [
    {
      header: "Vật tư",
      cell: (plan) => <span className="font-medium text-foreground">{plan.supplyName}</span>,
    },
    {
      header: "Lượng dự kiến",
      className: "text-right",
      cell: (plan) => (
        <span className="font-semibold text-foreground">
          {formatQuantity(plan.plannedQuantity, plan.unit)}
        </span>
      ),
    },
    {
      header: "Tồn hiện tại",
      className: "text-right",
      cell: (plan) => (
        <span className="text-muted-foreground">
          {formatQuantity(plan.currentStock, plan.unit)}
        </span>
      ),
    },
    {
      header: "Ngưỡng tối thiểu",
      className: "text-right",
      cell: (plan) => (
        <span className="text-muted-foreground">
          {formatQuantity(plan.minStockLevel, plan.unit)}
        </span>
      ),
    },
    {
      header: "Cảnh báo",
      cell: (plan) => renderWarningBadge(plan),
    },
    ...(isPlanned
      ? [
          {
            header: "Thao tác",
            className: "text-right",
            cell: (plan: SeasonSupplyPlanResponse) => (
              <div className="flex items-center justify-end gap-1">
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
            ),
          },
        ]
      : []),
  ];

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
        <DataList
          items={plans ?? []}
          getKey={(plan) => plan.id}
          title={(plan) => plan.supplyName}
          subtitle={(plan) => (
            <div className="space-y-1">
              <div className="flex flex-wrap gap-x-3 text-xs text-muted-foreground">
                <span>
                  Dự kiến: <strong className="text-foreground">{formatQuantity(plan.plannedQuantity, plan.unit)}</strong>
                </span>
                <span>Tồn: {formatQuantity(plan.currentStock, plan.unit)}</span>
                <span>Ngưỡng: {formatQuantity(plan.minStockLevel, plan.unit)}</span>
              </div>
              {isPlanned && (
                <div className="flex items-center gap-2 pt-1">
                  <Button
                    variant="outline"
                    size="sm"
                    className="h-7 px-2 text-xs"
                    onClick={() => handleOpenEdit(plan)}
                    aria-label={`Sửa lượng vật tư ${plan.supplyName}`}
                  >
                    <Edit2Icon className="mr-1 h-3 w-3" /> Sửa
                  </Button>
                  <Button
                    variant="ghost"
                    size="sm"
                    className="h-7 px-2 text-xs text-destructive hover:bg-destructive/10"
                    onClick={() => handleOpenDelete(plan)}
                    aria-label={`Xóa vật tư ${plan.supplyName}`}
                  >
                    <Trash2Icon className="mr-1 h-3 w-3" /> Xóa
                  </Button>
                </div>
              )}
            </div>
          )}
          badge={(plan) => renderWarningBadge(plan)}
          columns={columns}
        />
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

          <form onSubmit={addForm.handleSubmit(handleAddSubmit)} className="space-y-4">
            {addError ? <ErrorAlert error={addError} /> : null}

            <div className="space-y-2">
              <Label htmlFor="supply-select">Vật tư *</Label>
              <Controller
                control={addForm.control}
                name="supplyId"
                render={({ field }) => (
                  <SupplySelect
                    id="supply-select"
                    value={field.value}
                    onChange={field.onChange}
                    status="Active"
                  />
                )}
              />
              {addForm.formState.errors.supplyId && (
                <p className="text-sm font-medium text-destructive">
                  {addForm.formState.errors.supplyId.message}
                </p>
              )}
            </div>

            <div className="space-y-2">
              <Label htmlFor="planned-quantity">Lượng dự kiến *</Label>
              <Input
                id="planned-quantity"
                type="text"
                inputMode="decimal"
                placeholder="Ví dụ: 50"
                {...addForm.register("plannedQuantity")}
              />
              {addForm.formState.errors.plannedQuantity && (
                <p className="text-sm font-medium text-destructive">
                  {addForm.formState.errors.plannedQuantity.message}
                </p>
              )}
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setAddModalOpen(false)}>
                Quay lại
              </Button>
              <Button type="submit" disabled={createMutation.isPending}>
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

          <form onSubmit={editForm.handleSubmit(handleEditSubmit)} className="space-y-4">
            {editError ? <ErrorAlert error={editError} /> : null}

            <div className="space-y-2">
              <Label htmlFor="edit-planned-quantity">Lượng dự kiến mới *</Label>
              <Input
                id="edit-planned-quantity"
                type="text"
                inputMode="decimal"
                placeholder="Ví dụ: 50"
                {...editForm.register("plannedQuantity")}
              />
              {editForm.formState.errors.plannedQuantity && (
                <p className="text-sm font-medium text-destructive">
                  {editForm.formState.errors.plannedQuantity.message}
                </p>
              )}
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" onClick={() => setEditModalOpen(false)}>
                Quay lại
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
