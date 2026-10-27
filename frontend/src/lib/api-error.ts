import type { AxiosError } from "axios";

/** Phản hồi lỗi của backend theo RFC 9457, thêm `code`, `errors`, `details`. */
interface ProblemDetails {
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  details?: Record<string, unknown>;
}

export const OFFLINE_ERROR_MESSAGE = "Mất kết nối, dữ liệu chưa được gửi.";
export const UNEXPECTED_ERROR_MESSAGE = "Có lỗi xảy ra, vui lòng thử lại.";

interface ApiErrorInit {
  status: number;
  code: string | null;
  message: string;
  fieldErrors?: Record<string, string[]>;
  details?: Record<string, unknown> | null;
}

/**
 * Lỗi gọi API đã chuẩn hóa. `message` là câu backend viết, hiển thị nguyên văn.
 * Không có ProblemDetails thì `code` là `null`; mất mạng thì `status` là `0` (D-52).
 */
export class ApiError extends Error {
  readonly status: number;
  readonly code: string | null;
  /** Lỗi của từng trường khi `VALIDATION_ERROR`, khóa là tên trường camelCase. */
  readonly fieldErrors: Record<string, string[]>;
  readonly details: Record<string, unknown> | null;

  constructor(init: ApiErrorInit) {
    super(init.message);
    this.name = "ApiError";
    this.status = init.status;
    this.code = init.code;
    this.fieldErrors = init.fieldErrors ?? {};
    this.details = init.details ?? null;
  }
}

function isProblemDetails(data: unknown): data is ProblemDetails {
  return typeof data === "object" && data !== null && ("code" in data || "title" in data);
}

/** Đọc lỗi axios thành `ApiError`. */
export function toApiError(error: AxiosError): ApiError {
  if (!error.response) {
    return new ApiError({ status: 0, code: null, message: OFFLINE_ERROR_MESSAGE });
  }

  const { status, data } = error.response;
  if (!isProblemDetails(data)) {
    return new ApiError({ status, code: null, message: UNEXPECTED_ERROR_MESSAGE });
  }

  return new ApiError({
    status,
    code: data.code ?? null,
    message: data.detail ?? data.title ?? UNEXPECTED_ERROR_MESSAGE,
    fieldErrors: data.errors,
    details: data.details,
  });
}

/** Câu hiển thị cho một lỗi bất kỳ: lỗi API dùng nguyên văn câu backend viết, lỗi khác dùng câu chung. */
export function getErrorMessage(error: unknown): string {
  return error instanceof ApiError ? error.message : UNEXPECTED_ERROR_MESSAGE;
}

/** Một bản ghi bị ảnh hưởng, liệt kê trong hộp thoại xác nhận (BR-GEN-14). */
export interface ConfirmationItem {
  id: string;
  label: string;
}

function isConfirmationItem(value: unknown): value is ConfirmationItem {
  return (
    typeof value === "object" &&
    value !== null &&
    "id" in value &&
    typeof value.id === "string" &&
    "label" in value &&
    typeof value.label === "string"
  );
}

/** `details.items` của lỗi `CONFIRMATION_REQUIRED`; lỗi khác hoặc dữ liệu sai dạng thì trả mảng rỗng. */
export function getConfirmationItems(error: unknown): ConfirmationItem[] {
  if (!(error instanceof ApiError) || error.code !== "CONFIRMATION_REQUIRED") {
    return [];
  }
  const items = error.details?.items;
  return Array.isArray(items) ? items.filter(isConfirmationItem) : [];
}
