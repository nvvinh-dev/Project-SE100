import type { FieldValues, Path, UseFormSetError } from "react-hook-form";
import { ApiError } from "@/lib/api-error";

/**
 * Gắn lỗi `VALIDATION_ERROR` của backend vào đúng ô của form.
 * Chỉ gắn cho các ô có trong `fields`; trả về `true` nếu đã gắn được ít nhất một lỗi,
 * `false` thì bên gọi hiện lỗi chung trên form.
 */
export function setFieldErrors<T extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<T>,
  fields: readonly Path<T>[],
): boolean {
  if (!(error instanceof ApiError) || error.code !== "VALIDATION_ERROR") {
    return false;
  }

  let applied = false;
  for (const field of fields) {
    const [message] = error.fieldErrors[field] ?? [];
    if (message) {
      setError(field, { type: "server", message });
      applied = true;
    }
  }
  return applied;
}
