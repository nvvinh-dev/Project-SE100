import { afterEach, describe, expect, it, vi } from "vitest";
import { newRequestId, uuidFromRandomBytes } from "@/lib/request-id";

const UUID_V4 = /^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/;

describe("newRequestId", () => {
  afterEach(() => {
    vi.unstubAllGlobals();
  });

  it("sinh UUID phiên bản 4, mỗi lần một giá trị", () => {
    const first = newRequestId();
    expect(first).toMatch(UUID_V4);
    expect(newRequestId()).not.toBe(first);
  });

  it("vẫn sinh được khi không có crypto.randomUUID (trang mở qua HTTP)", () => {
    vi.stubGlobal("crypto", { getRandomValues: (bytes: Uint8Array) => bytes.fill(0xab) });
    expect(newRequestId()).toBe("abababab-abab-4bab-abab-abababababab");
  });
});

describe("uuidFromRandomBytes", () => {
  it("đặt đúng bit phiên bản và biến thể", () => {
    expect(uuidFromRandomBytes(new Uint8Array(16))).toBe("00000000-0000-4000-8000-000000000000");
    expect(uuidFromRandomBytes(new Uint8Array(16).fill(0xff))).toBe("ffffffff-ffff-4fff-bfff-ffffffffffff");
  });

  it("không sửa mảng byte truyền vào", () => {
    const bytes = new Uint8Array(16).fill(0xff);
    uuidFromRandomBytes(bytes);
    expect(Array.from(bytes)).toEqual(new Array(16).fill(0xff));
  });
});
