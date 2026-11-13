import { Loader2Icon, PencilIcon, PlusIcon } from "lucide-react";
import { useState } from "react";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
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
import { Textarea } from "@/components/ui/textarea";
import { useCreateSeasonNote, useSeasonNotes, useUpdateSeasonNote } from "@/features/seasons/queries";
import type { SeasonNoteResponse } from "@/features/seasons/types";

interface SeasonNotesTabProps {
  seasonId: string;
}

export function SeasonNotesTab({ seasonId }: SeasonNotesTabProps) {
  const { data: notes, isLoading } = useSeasonNotes(seasonId);
  const createMutation = useCreateSeasonNote(seasonId);
  const updateMutation = useUpdateSeasonNote(seasonId);

  const [newContent, setNewContent] = useState("");
  const [editingNote, setEditingNote] = useState<SeasonNoteResponse | null>(null);
  const [editContent, setEditContent] = useState("");

  const handleCreate = async () => {
    if (!newContent.trim()) return;
    try {
      await createMutation.mutateAsync({
        seasonId,
        content: newContent.trim(),
      });
      setNewContent("");
      showSuccess("Đã thêm ghi chú kinh nghiệm.");
    } catch (err) {
      showError(err);
    }
  };

  const handleStartEdit = (note: SeasonNoteResponse) => {
    setEditingNote(note);
    setEditContent(note.content);
  };

  const handleUpdate = async () => {
    if (!editingNote || !editContent.trim()) return;
    try {
      await updateMutation.mutateAsync({
        id: editingNote.id,
        request: {
          content: editContent.trim(),
          version: editingNote.version,
        },
      });
      setEditingNote(null);
      showSuccess("Đã cập nhật ghi chú kinh nghiệm.");
    } catch (err) {
      showError(err);
    }
  };

  if (isLoading) {
    return <div className="p-4 text-center text-sm text-muted-foreground">Đang tải ghi chú...</div>;
  }

  return (
    <div className="space-y-4">
      {/* Form thêm ghi chú mới */}
      <div className="rounded-lg border bg-card p-4 space-y-3">
        <h4 className="text-sm font-semibold">Thêm ghi chú kinh nghiệm</h4>
        <Textarea
          placeholder="Nhập ghi chú kinh nghiệm, thời tiết, kỹ thuật chăm sóc..."
          value={newContent}
          onChange={(e) => setNewContent(e.target.value)}
          rows={3}
        />
        <div className="flex justify-end">
          <Button
            size="sm"
            onClick={handleCreate}
            disabled={!newContent.trim() || createMutation.isPending}
          >
            {createMutation.isPending ? (
              <Loader2Icon className="mr-2 h-4 w-4 animate-spin" />
            ) : (
              <PlusIcon className="mr-2 h-4 w-4" />
            )}
            Lưu ghi chú
          </Button>
        </div>
      </div>

      {/* Danh sách ghi chú */}
      {!notes || notes.length === 0 ? (
        <EmptyState
          message="Chưa có ghi chú nào. Hãy ghi lại kinh nghiệm canh tác để lưu trữ và cải thiện cho các mùa vụ sau."
        />
      ) : (
        <div className="space-y-3">
          {notes.map((note) => (
            <div key={note.id} className="rounded-lg border bg-card p-4 space-y-2">
              <div className="flex items-center justify-between text-xs text-muted-foreground">
                <span className="font-medium text-foreground">{note.authorName}</span>
                <div className="flex items-center gap-2">
                  <DateText value={note.createdAt} />
                  {note.updatedAt && <span className="italic">(đã sửa)</span>}
                  {note.canEdit && (
                    <Button
                      variant="ghost"
                      size="icon"
                      className="h-6 w-6"
                      onClick={() => handleStartEdit(note)}
                    >
                      <PencilIcon className="h-3.5 w-3.5" />
                      <span className="sr-only">Sửa</span>
                    </Button>
                  )}
                </div>
              </div>
              <p className="whitespace-pre-wrap text-sm">{note.content}</p>
            </div>
          ))}
        </div>
      )}

      {/* Modal sửa ghi chú */}
      <Dialog open={editingNote !== null} onOpenChange={(open) => !open && setEditingNote(null)}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Sửa ghi chú kinh nghiệm</DialogTitle>
            <DialogDescription>Chỉ người viết ghi chú mới có thể chỉnh sửa nội dung này.</DialogDescription>
          </DialogHeader>
          <div className="py-2">
            <Textarea
              value={editContent}
              onChange={(e) => setEditContent(e.target.value)}
              rows={4}
            />
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setEditingNote(null)}
              disabled={updateMutation.isPending}
            >
              Hủy
            </Button>
            <Button
              onClick={handleUpdate}
              disabled={!editContent.trim() || updateMutation.isPending}
            >
              {updateMutation.isPending && (
                <Loader2Icon className="mr-2 h-4 w-4 animate-spin" />
              )}
              Lưu thay đổi
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
