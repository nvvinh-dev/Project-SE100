import type { ExportReportRequest } from "@/features/reports/common/types";
import { apiClient } from "@/lib/api-client";
import { ApiError, UNEXPECTED_ERROR_MESSAGE } from "@/lib/api-error";

interface ProblemDetailsPayload {
  title?: string;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
  details?: Record<string, unknown>;
}

/**
 * Tách tên tệp từ header Content-Disposition, ưu tiên `filename*=UTF-8''...` để giữ tiếng Việt có dấu.
 */
export function parseContentDispositionFileName(headerValue: string | undefined, fallbackFileName = "bao-cao.xlsx"): string {
  if (!headerValue) {
    return fallbackFileName;
  }

  const utf8Match = /filename\*=UTF-8''([^;\r\n]+)/i.exec(headerValue);
  if (utf8Match?.[1]) {
    try {
      return decodeURIComponent(utf8Match[1].trim().replace(/^"|"$/g, ""));
    } catch {
      return utf8Match[1].trim();
    }
  }

  const asciiMatch = /filename="?([^";\r\n]+)"?/i.exec(headerValue);
  if (asciiMatch?.[1]) {
    return asciiMatch[1].trim();
  }

  return fallbackFileName;
}

async function parseBlobProblemError(status: number, blob: Blob): Promise<ApiError> {
  try {
    const text = await blob.text();
    const parsed = JSON.parse(text) as ProblemDetailsPayload;
    if (typeof parsed === "object" && parsed !== null && ("code" in parsed || "title" in parsed || "detail" in parsed)) {
      return new ApiError({
        status,
        code: parsed.code ?? null,
        message: parsed.detail ?? parsed.title ?? UNEXPECTED_ERROR_MESSAGE,
        fieldErrors: parsed.errors,
        details: parsed.details,
      });
    }
  } catch {
    // Phản hồi lỗi không phải JSON ProblemDetails thì rơi về thông báo chung.
  }

  return new ApiError({ status, code: null, message: UNEXPECTED_ERROR_MESSAGE });
}

/**
 * Gọi API xuất báo cáo dạng tệp (FR-RPT-04, FR-RPT-06) qua apiClient và tải xuống trình duyệt.
 * Giữ lỗi 400/404/422 đi qua validateStatus để đọc nguyên văn `detail` từ ProblemDetails trong Blob.
 */
export async function downloadReportFile(
  endpoint: string,
  request: ExportReportRequest,
): Promise<{ fileName: string }> {
  const response = await apiClient.post<Blob>(endpoint, request, {
    responseType: "blob",
    validateStatus: (status) =>
      (status >= 200 && status < 300) || status === 400 || status === 404 || status === 422,
  });

  if (response.status >= 400) {
    throw await parseBlobProblemError(response.status, response.data);
  }

  const fallbackName = request.format === "Pdf" ? "bao-cao.pdf" : "bao-cao.xlsx";
  const contentDisposition =
    typeof response.headers["content-disposition"] === "string"
      ? response.headers["content-disposition"]
      : undefined;
  const fileName = parseContentDispositionFileName(contentDisposition, fallbackName);

  const objectUrl = URL.createObjectURL(response.data);
  try {
    const anchor = document.createElement("a");
    anchor.href = objectUrl;
    anchor.download = fileName;
    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();
  } finally {
    URL.revokeObjectURL(objectUrl);
  }

  return { fileName };
}
