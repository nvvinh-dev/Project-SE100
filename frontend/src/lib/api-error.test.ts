import { AxiosError, AxiosHeaders, type InternalAxiosRequestConfig } from "axios";
import { describe, expect, it } from "vitest";
import { OFFLINE_ERROR_MESSAGE, toApiError, UNEXPECTED_ERROR_MESSAGE } from "@/lib/api-error";

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
