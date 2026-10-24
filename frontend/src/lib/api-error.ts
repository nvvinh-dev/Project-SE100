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
