import { describe, expect, it } from "vitest";
import { parseContentDispositionFileName } from "@/features/reports/common/api";

describe("parseContentDispositionFileName", () => {
  it("ưu tiên đọc filename*=UTF-8 khi có tên tiếng Việt có dấu", () => {
    const header = "attachment; filename=\"bao-cao.xlsx\"; filename*=UTF-8''b%C3%A1o-c%C3%A1o-s%E1%BA%A3n-xu%E1%BA%A5t.xlsx";
    expect(parseContentDispositionFileName(header)).toBe("báo-cáo-sản-xuất.xlsx");
  });

  it("đọc filename ASCII khi không có filename*", () => {
    const header = 'attachment; filename="bao-cao-san-xuat-20261113-171530.xlsx"';
    expect(parseContentDispositionFileName(header)).toBe("bao-cao-san-xuat-20261113-171530.xlsx");
  });

  it("trả tên mặc định khi thiếu header", () => {
    expect(parseContentDispositionFileName(undefined, "bao-cao.pdf")).toBe("bao-cao.pdf");
  });
});
