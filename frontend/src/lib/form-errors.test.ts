import { describe, expect, it, vi } from "vitest";
import { ApiError } from "@/lib/api-error";
import { setFieldErrors } from "@/lib/form-errors";

interface Values {
  phone: string;
  password: string;
}

const fields = ["phone", "password"] as const;

function validationError(fieldErrors: Record<string, string[]>) {
  return new ApiError({ status: 400, code: "VALIDATION_ERROR", message: "Dữ liệu chưa hợp lệ.", fieldErrors });
}

describe("setFieldErrors", () => {
  it("gắn câu lỗi đầu tiên của từng ô có trong form", () => {
    const setError = vi.fn();

    const applied = setFieldErrors<Values>(
      validationError({ phone: ["Số điện thoại sai.", "Câu thứ hai."], password: ["Thiếu mật khẩu."] }),
      setError,
      fields,
    );

    expect(applied).toBe(true);
    expect(setError).toHaveBeenCalledWith("phone", { type: "server", message: "Số điện thoại sai." });
    expect(setError).toHaveBeenCalledWith("password", { type: "server", message: "Thiếu mật khẩu." });
  });

  it("bỏ qua lỗi của trường không có trong form", () => {
    const setError = vi.fn();

    const applied = setFieldErrors<Values>(validationError({ requestId: ["Thiếu mã yêu cầu."] }), setError, fields);

    expect(applied).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });

  it("không xử lý lỗi khác VALIDATION_ERROR", () => {
    const setError = vi.fn();
    const error = new ApiError({ status: 401, code: "INVALID_CREDENTIALS", message: "Sai mật khẩu." });

    expect(setFieldErrors<Values>(error, setError, fields)).toBe(false);
    expect(setFieldErrors<Values>(new Error("khác"), setError, fields)).toBe(false);
    expect(setError).not.toHaveBeenCalled();
  });
});
