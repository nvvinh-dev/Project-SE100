import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from "axios";
import { describe, expect, it } from "vitest";
import {
  ApiError,
  getConfirmationItems,
  getErrorMessage,
  OFFLINE_ERROR_MESSAGE,
  toApiError,
  UNEXPECTED_ERROR_MESSAGE,
} from "@/lib/api-error";

const config: InternalAxiosRequestConfig = { headers: new AxiosHeaders() };

function errorWithResponse(status: number, data: unknown): AxiosError {
  return new AxiosError("Request failed", AxiosError.ERR_BAD_RESPONSE, config, undefined, {
    status,
    statusText: "",
    headers: {},
    config,
    data,
  });
}

describe("toApiError", () => {
  it("đọc ProblemDetails lỗi nghiệp vụ", () => {
    const error = toApiError(
      errorWithResponse(422, {
        title: "Không đủ tồn kho",
        status: 422,
        detail: "Phân NPK chỉ còn 5 kg, không đủ để xuất 8 kg.",
        code: "INSUFFICIENT_STOCK",
        details: { available: 5, requested: 8 },
        traceId: "00-abc-01",
      }),
    );

    expect(error.status).toBe(422);
    expect(error.code).toBe("INSUFFICIENT_STOCK");
    expect(error.message).toBe("Phân NPK chỉ còn 5 kg, không đủ để xuất 8 kg.");
    expect(error.details).toEqual({ available: 5, requested: 8 });
    expect(error.fieldErrors).toEqual({});
  });

  it("đọc lỗi từng trường của VALIDATION_ERROR, không có detail thì dùng title", () => {
    const error = toApiError(
      errorWithResponse(400, {
        title: "Dữ liệu không hợp lệ",
        status: 400,
        code: "VALIDATION_ERROR",
        errors: { areaM2: ["Diện tích phải lớn hơn 0."] },
      }),
    );

    expect(error.code).toBe("VALIDATION_ERROR");
    expect(error.message).toBe("Dữ liệu không hợp lệ");
    expect(error.fieldErrors).toEqual({ areaM2: ["Diện tích phải lớn hơn 0."] });
    expect(error.details).toBeNull();
  });

  it("ProblemDetails không có code (405 của framework) thì code là null", () => {
    const error = toApiError(errorWithResponse(405, { title: "Method Not Allowed", status: 405 }));

    expect(error.status).toBe(405);
    expect(error.code).toBeNull();
    expect(error.message).toBe("Method Not Allowed");
  });

  it("phản hồi không phải ProblemDetails thì dùng câu chung", () => {
    const error = toApiError(errorWithResponse(502, ""));

    expect(error.status).toBe(502);
    expect(error.code).toBeNull();
    expect(error.message).toBe(UNEXPECTED_ERROR_MESSAGE);
  });

  it("không nhận được phản hồi (mất mạng) thì status là 0", () => {
    const error = toApiError(new AxiosError("Network Error", AxiosError.ERR_NETWORK, config));

    expect(error.status).toBe(0);
    expect(error.code).toBeNull();
    expect(error.message).toBe(OFFLINE_ERROR_MESSAGE);
  });
});

describe("getErrorMessage", () => {
  it("lỗi API thì trả nguyên văn câu của backend", () => {
    const error = new ApiError({ status: 403, code: "FORBIDDEN", message: "Cần quyền Quản lý kho." });

    expect(getErrorMessage(error)).toBe("Cần quyền Quản lý kho.");
  });

  it("lỗi khác thì trả câu chung, không lộ lỗi kỹ thuật", () => {
    expect(getErrorMessage(new TypeError("Cannot read properties of undefined"))).toBe(UNEXPECTED_ERROR_MESSAGE);
    expect(getErrorMessage("lỗi")).toBe(UNEXPECTED_ERROR_MESSAGE);
  });
});

describe("getConfirmationItems", () => {
  function confirmationError(details: Record<string, unknown> | null): ApiError {
    return new ApiError({
      status: 422,
      code: "CONFIRMATION_REQUIRED",
      message: "Các công việc chưa làm dưới đây sẽ bị hủy khi chốt mùa vụ.",
      details,
    });
  }

  it("đọc danh sách bản ghi bị ảnh hưởng", () => {
    const error = confirmationError({
      items: [
        { id: "a1", label: "Tưới nước - 28/09/2026" },
        { id: "a2", label: "Bón phân - 30/09/2026" },
      ],
    });

    expect(getConfirmationItems(error)).toEqual([
      { id: "a1", label: "Tưới nước - 28/09/2026" },
      { id: "a2", label: "Bón phân - 30/09/2026" },
    ]);
  });

  it("bỏ qua phần tử sai dạng", () => {
    const error = confirmationError({ items: [{ id: "a1", label: "Tưới nước" }, { id: 2 }, null, "x"] });

    expect(getConfirmationItems(error)).toEqual([{ id: "a1", label: "Tưới nước" }]);
  });

  it("không có details hoặc items không phải mảng thì trả mảng rỗng", () => {
    expect(getConfirmationItems(confirmationError(null))).toEqual([]);
    expect(getConfirmationItems(confirmationError({ items: "a1" }))).toEqual([]);
  });

  it("lỗi không phải CONFIRMATION_REQUIRED thì trả mảng rỗng", () => {
    const error = new ApiError({
      status: 422,
      code: "INSUFFICIENT_STOCK",
      message: "Không đủ tồn kho.",
      details: { items: [{ id: "a1", label: "Phân NPK" }] },
    });

    expect(getConfirmationItems(error)).toEqual([]);
    expect(getConfirmationItems(new Error("x"))).toEqual([]);
  });
});
