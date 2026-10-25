import axios, { isAxiosError, isCancel } from "axios";
import { toApiError } from "@/lib/api-error";
import { getAccessToken } from "@/lib/auth/token-store";

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

apiClient.interceptors.request.use((config) => {
  const token = getAccessToken();
  if (token) {
    config.headers.set("Authorization", `Bearer ${token}`);
  }
  return config;
});

apiClient.interceptors.response.use(undefined, (error: unknown) => {
  // Yêu cầu bị hủy (TanStack Query hủy khi rời trang) không phải lỗi để báo cho người dùng.
  if (isAxiosError(error) && !isCancel(error)) {
    return Promise.reject(toApiError(error));
  }
  return Promise.reject(error);
});
