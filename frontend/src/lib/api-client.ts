import axios, { isAxiosError, isCancel, type InternalAxiosRequestConfig } from "axios";
import { type ApiError, toApiError } from "@/lib/api-error";
import { getAccessToken, setAccessToken } from "@/lib/auth/token-store";
import type { AccessTokenResponse } from "@/lib/auth/types";

/** Phản hồi thành công của mọi API: `{ data, meta?, warnings }` (D-32). */
export interface ResponseEnvelope<T> {
  data: T;
  meta?: PageMeta;
  warnings: ResponseWarning[];
}

/** Thông tin phân trang, chỉ có ở API danh sách. */
export interface PageMeta {
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface ResponseWarning {
  code: string;
  message: string;
}

/** Tham số chung của API danh sách; tham số lọc của module mở rộng kiểu này. */
export interface PageQuery {
  page?: number;
  pageSize?: number;
  /** Tên trường camelCase, thêm `-` phía trước để sắp giảm dần. */
  sort?: string;
  search?: string;
}

/** Instance axios duy nhất của ứng dụng; luôn gọi đường dẫn tương đối /api (D-13). */
export const apiClient = axios.create({ baseURL: "/api" });

// Các yêu cầu không dùng access token: lỗi ở đây là kết quả của chính thao tác (sai mật khẩu, tài khoản bị khóa
// khi đăng nhập, refresh token hết hạn), không phải dấu hiệu phiên đang dùng đã hết.
const REQUESTS_WITHOUT_SESSION = ["/auth/login", "/auth/register", "/auth/refresh", "/auth/logout"];

interface RetriableRequestConfig extends InternalAxiosRequestConfig {
  retriedAfterRefresh?: boolean;
}

let refreshing: Promise<boolean> | null = null;
let onSessionEnded: ((error?: ApiError) => void) | null = null;

/**
 * Đăng ký việc cần làm khi phiên kết thúc giữa chừng: làm mới phiên thất bại, hoặc tài khoản bị khóa.
 * `AuthProvider` đăng ký để xóa token, xóa dữ liệu đã tải và đưa về trang đăng nhập.
 */
export function setSessionEndedHandler(handler: ((error?: ApiError) => void) | null): void {
  onSessionEnded = handler;
}

/**
 * Lấy access token mới bằng cookie refresh token (FR-AUTH-03). Nhiều yêu cầu cùng lúc dùng chung một lần làm mới:
 * mỗi refresh token chỉ dùng được một lần, gửi lại token vừa bị thay thì backend thu hồi mọi phiên (BR-USR-05).
 */
export function refreshAccessToken(): Promise<boolean> {
  refreshing ??= apiClient
    .post<ResponseEnvelope<AccessTokenResponse>>("/auth/refresh")
    .then((response) => {
      setAccessToken(response.data.data.accessToken);
      return true;
    })
    .catch(() => {
      setAccessToken(null);
      return false;
    })
    .finally(() => {
      refreshing = null;
    });
  return refreshing;
}

apiClient.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.set("Authorization", `Bearer ${token}`);
  }
  return config;
});

apiClient.interceptors.response.use(undefined, async (error: unknown) => {
  // Yêu cầu bị hủy (TanStack Query hủy khi rời trang) không phải lỗi để báo cho người dùng.
  if (!isAxiosError(error) || isCancel(error)) {
    return Promise.reject(error);
  }

  const apiError = toApiError(error);
  const config = error.config as RetriableRequestConfig | undefined;
  const usesSession = config !== undefined && !REQUESTS_WITHOUT_SESSION.includes(config.url ?? "");

  if (usesSession && apiError.code === "UNAUTHENTICATED") {
    // FR-AUTH-03: access token hết hạn khi đang thao tác thì làm mới phiên rồi gửi lại, không đưa ra trang đăng nhập.
    if (!config.retriedAfterRefresh && (await refreshAccessToken())) {
      config.retriedAfterRefresh = true;
      return apiClient(config);
    }
    onSessionEnded?.();
  } else if (usesSession && apiError.code === "ACCOUNT_LOCKED") {
    // BR-GEN-04: tài khoản bị khóa có hiệu lực ngay ở yêu cầu kế tiếp (D-11), nên kết thúc phiên và báo lý do.
    onSessionEnded?.(apiError);
  }

  return Promise.reject(apiError);
});
