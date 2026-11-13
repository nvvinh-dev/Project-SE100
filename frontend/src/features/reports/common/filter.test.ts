import { describe, expect, it } from "vitest";
import { applyFilterParams, readReportFilter } from "@/features/reports/common/filter";

describe("report filter URL helpers", () => {
  it("đọc đúng bộ lọc chung từ URLSearchParams", () => {
    const params = new URLSearchParams("from=2026-10-01&to=2026-11-13&plotId=p1");
    expect(readReportFilter(params)).toEqual({
      from: "2026-10-01",
      to: "2026-11-13",
      seasonId: undefined,
      plotId: "p1",
      cropId: undefined,
    });
  });

  it("cập nhật và xóa tham số rỗng trên URLSearchParams", () => {
    const initial = new URLSearchParams("from=2026-10-01&plotId=p1");
    const updated = applyFilterParams(initial, { from: undefined, cropId: "c1" });
    expect(updated.toString()).toBe("plotId=p1&cropId=c1");
  });
});
