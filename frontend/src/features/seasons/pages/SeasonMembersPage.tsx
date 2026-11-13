import { Loader2Icon, PlusIcon, Trash2Icon, UserPlusIcon } from "lucide-react";
import { useState } from "react";
import { Link, useParams } from "react-router";
import { ConfirmDialog } from "@/components/common/ConfirmDialog";
import { DateText } from "@/components/common/DateText";
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
import { NativeSelect } from "@/components/ui/native-select";
import {
  useAssignSeason,
  useHouseholdMembers,
  useSeasonAssignments,
  useSeasonDetail,
  useUnassignSeason,
} from "@/features/seasons/queries";
import type { SeasonAssignmentItem } from "@/features/seasons/types";
import { getConfirmationItems, type ConfirmationItem } from "@/lib/api-error";

export function SeasonMembersPage() {
  const { id = "" } = useParams();
  const { data: season, isLoading: isSeasonLoading } = useSeasonDetail(id);
  const { data: assignments, isLoading: isAssignmentsLoading, error } = useSeasonAssignments(id);
  const { data: members, isLoading: isMembersLoading } = useHouseholdMembers();

  const assignMutation = useAssignSeason(id);
  const unassignMutation = useUnassignSeason(id);

  // Modal thêm người
  const [addModalOpen, setAddModalOpen] = useState(false);
  const [selectedUserId, setSelectedUserId] = useState("");

  // Modal xác nhận thu hồi phân công (BR-GEN-14, BR-USR-12)
  const [confirmUnassignOpen, setConfirmUnassignOpen] = useState(false);
  const [unassignTarget, setUnassignTarget] = useState<SeasonAssignmentItem | null>(null);
  const [confirmItems, setConfirmItems] = useState<ConfirmationItem[]>([]);
  const [confirmDescription, setConfirmDescription] = useState<string>("");

  if (isSeasonLoading || isAssignmentsLoading) {
    return <LoadingState variant="list" />;
  }

  if (error || !season) {
    return (
      <div className="space-y-4 p-4">
        <PageHeader title="Phân công thành viên" backTo={`/seasons/${id}`} />
        <ErrorAlert error={error ?? new Error("Không tìm thấy mùa vụ")} />
      </div>
    );
  }

  // Danh sách người khả dụng để phân công: không phải Owner, chưa bị khóa, chưa được phân công
  const assignedUserIds = new Set(assignments?.map((a) => a.userId) ?? []);
  const availableMembers = (members ?? []).filter(
    (m) => m.role !== "Owner" && !m.isLocked && !assignedUserIds.has(m.id),
  );

  const handleAssign = async () => {
    if (!selectedUserId) return;
    try {
      await assignMutation.mutateAsync({
        seasonId: id,
        userId: selectedUserId,
      });
      setAddModalOpen(false);
      setSelectedUserId("");
      showSuccess("Đã phân công người vào mùa vụ.");
    } catch (err) {
      showError(err);
    }
  };

  const handleStartUnassign = async (assignment: SeasonAssignmentItem) => {
    setUnassignTarget(assignment);
    try {
      await unassignMutation.mutateAsync({
        id: assignment.id,
        request: { confirmed: false },
      });
      showSuccess(`Đã thu hồi phân công của ${assignment.fullName}.`);
      setUnassignTarget(null);
    } catch (err: unknown) {
      const items = getConfirmationItems(err);
      if (items.length > 0) {
        setConfirmItems(items);
        setConfirmDescription(
          `${assignment.fullName} còn công việc chưa hoàn thành trong mùa vụ. Sau khi xác nhận, các công việc này sẽ được chuyển về trạng thái chưa giao người.`,
        );
        setConfirmUnassignOpen(true);
      } else {
        showError(err);
        setUnassignTarget(null);
      }
    }
  };

  const handleConfirmUnassign = async () => {
    if (!unassignTarget) return;
    try {
      await unassignMutation.mutateAsync({
        id: unassignTarget.id,
        request: { confirmed: true },
      });
      setConfirmUnassignOpen(false);
      setUnassignTarget(null);
      setConfirmItems([]);
      showSuccess(`Đã thu hồi phân công của ${unassignTarget.fullName}.`);
    } catch (err) {
      showError(err);
    }
  };

  return (
    <div className="space-y-4 pb-12">
      <PageHeader
        title="Phân công thành viên"
        description={`${season.cropName} · ${season.plotCode}`}
        backTo={`/seasons/${id}`}
        action={
          <Button size="sm" onClick={() => setAddModalOpen(true)}>
            <UserPlusIcon className="mr-1.5 h-4 w-4" /> Thêm người
          </Button>
        }
      />

      {/* Danh sách phân công */}
      {!assignments || assignments.length === 0 ? (
        <EmptyState
          message="Chưa có người được phân công. Chủ hộ phân công Thành viên hộ hoặc Nhân công để họ có thể xem và thực hiện công việc của mùa vụ này."
          action={
            <Button onClick={() => setAddModalOpen(true)}>
              <PlusIcon className="mr-1.5 h-4 w-4" /> Phân công ngay
            </Button>
          }
        />
      ) : (
        <div className="space-y-2">
          {assignments.map((assignment) => (
            <div
              key={assignment.id}
              className="flex items-center justify-between rounded-lg border bg-card p-3.5"
            >
              <div className="space-y-0.5">
                <div className="flex items-center gap-2">
                  <span className="font-semibold text-sm">{assignment.fullName}</span>
                  <span className="rounded-full bg-secondary px-2 py-0.5 text-[10px] font-medium">
                    {assignment.role === "Member" ? "Thành viên" : "Nhân công"}
                  </span>
                </div>
                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  {assignment.phoneNumber && <span>{assignment.phoneNumber}</span>}
                  <span>·</span>
                  <span>
                    Phân công: <DateText value={assignment.createdAt} />
                  </span>
                </div>
              </div>
              <Button
                variant="ghost"
                size="icon"
                className="text-muted-foreground hover:text-destructive"
                onClick={() => handleStartUnassign(assignment)}
                disabled={unassignMutation.isPending}
              >
                <Trash2Icon className="h-4 w-4" />
                <span className="sr-only">Thu hồi</span>
              </Button>
            </div>
          ))}
        </div>
      )}

      {/* Modal Thêm người vào mùa vụ */}
      <Dialog open={addModalOpen} onOpenChange={setAddModalOpen}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Thêm người vào mùa vụ</DialogTitle>
            <DialogDescription>
              Chọn Thành viên hộ hoặc Nhân công để cấp quyền làm việc trong mùa vụ này.
            </DialogDescription>
          </DialogHeader>

          {isMembersLoading ? (
            <div className="py-4 text-center text-sm text-muted-foreground">
              Đang tải danh sách thành viên...
            </div>
          ) : availableMembers.length === 0 ? (
            <div className="py-4 text-center space-y-2 text-sm text-muted-foreground">
              <p>Không có thành viên hoặc nhân công nào khả dụng để phân công.</p>
              <Button variant="link" size="sm" asChild>
                <Link to="/members/new">Tạo tài khoản thành viên mới ›</Link>
              </Button>
            </div>
          ) : (
            <div className="py-3 space-y-3">
              <label className="text-sm font-medium">Chọn người phân công</label>
              <NativeSelect
                value={selectedUserId}
                onChange={(e) => setSelectedUserId(e.target.value)}
              >
                <option value="">-- Chọn thành viên hoặc nhân công --</option>
                {availableMembers.map((m) => (
                  <option key={m.id} value={m.id}>
                    {m.fullName} ({m.role === "Member" ? "Thành viên" : "Nhân công"})
                    {m.phoneNumber ? ` - ${m.phoneNumber}` : ""}
                  </option>
                ))}
              </NativeSelect>
            </div>
          )}

          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setAddModalOpen(false)}
              disabled={assignMutation.isPending}
            >
              Hủy
            </Button>
            <Button
              onClick={handleAssign}
              disabled={!selectedUserId || assignMutation.isPending}
            >
              {assignMutation.isPending && (
                <Loader2Icon className="mr-2 h-4 w-4 animate-spin" />
              )}
              Phân công
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>

      {/* Dialog xác nhận 2 bước khi có việc chưa làm (BR-GEN-14, BR-USR-12) */}
      <ConfirmDialog
        open={confirmUnassignOpen}
        onOpenChange={setConfirmUnassignOpen}
        title="Thu hồi phân công?"
        description={confirmDescription}
        items={confirmItems}
        confirmLabel="Xác nhận thu hồi"
        onConfirm={handleConfirmUnassign}
        pending={unassignMutation.isPending}
        destructive
      />
    </div>
  );
}
