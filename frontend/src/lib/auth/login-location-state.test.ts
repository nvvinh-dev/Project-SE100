import { describe, expect, it } from "vitest";
import { readLoginLocationState } from "@/lib/auth/login-location-state";

describe("readLoginLocationState", () => {
  it("đọc trang đang mở và số điện thoại", () => {
    expect(readLoginLocationState({ from: "/seasons/1?tab=tasks", phone: "0912345678" })).toEqual({
      from: "/seasons/1?tab=tasks",
      phone: "0912345678",
    });
  });

  it.each([null, undefined, "chuỗi", 42])("state %j thì trả rỗng", (state) => {
    expect(readLoginLocationState(state)).toEqual({});
  });

  it.each(["https://trang-khac.vn", "//trang-khac.vn", "seasons", 42])(
    "bỏ đường dẫn không thuộc ứng dụng %j",
    (from) => {
      expect(readLoginLocationState({ from }).from).toBeUndefined();
    },
  );
});
