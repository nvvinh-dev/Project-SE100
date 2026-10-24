import { describe, expect, it } from "vitest";
import { formatArea, formatDate, formatDateTime, formatMoney, formatQuantity, parseDecimalInput } from "@/lib/format";

// Intl dùng khoảng trắng không ngắt dòng giữa số và đơn vị.
const NBSP = " ";

describe("formatMoney", () => {
  it.each([
    [1250000, `1.250.000${NBSP}₫`],
    [0, `0${NBSP}₫`],
    [-1250000, `-1.250.000${NBSP}₫`],
    [1250000.6, `1.250.001${NBSP}₫`],
    [-0.4, `0${NBSP}₫`],
  ])("%d → %s", (amount, expected) => {
    expect(formatMoney(amount)).toBe(expected);
  });
});

describe("formatQuantity", () => {
  it.each([
    [12.5, "Kilogram", `12,5${NBSP}kg`],
    [1500, "Kilogram", `1.500${NBSP}kg`],
    [0.125, "Liter", `0,125${NBSP}lít`],
    [2.0004, "Ton", `2${NBSP}tấn`],
    [3, "Quintal", `3${NBSP}tạ`],
  ] as const)("%d %s → %s", (value, unit, expected) => {
    expect(formatQuantity(value, unit)).toBe(expected);
  });
});

describe("formatArea", () => {
  it("mặc định hiển thị m²", () => {
    expect(formatArea(1500)).toBe(`1.500${NBSP}m²`);
    expect(formatArea(1234.5)).toBe(`1.234,5${NBSP}m²`);
  });

  it("đổi sang ha cho báo cáo năng suất", () => {
    expect(formatArea(1500, "ha")).toBe(`0,15${NBSP}ha`);
    expect(formatArea(25000, "ha")).toBe(`2,5${NBSP}ha`);
  });
});

describe("formatDate", () => {
  it("đổi yyyy-MM-dd sang dd/MM/yyyy", () => {
    expect(formatDate("2026-09-26")).toBe("26/09/2026");
    expect(formatDate("2027-01-01")).toBe("01/01/2027");
  });

  it("giữ nguyên chuỗi không đúng dạng ngày", () => {
    expect(formatDate("26/09/2026")).toBe("26/09/2026");
  });
});

describe("formatDateTime", () => {
  it.each([
    ["2026-09-26T07:30:00Z", "26/09/2026 14:30"],
    // Sau 17 giờ UTC đã sang ngày hôm sau theo giờ Việt Nam.
    ["2026-09-26T17:30:00Z", "27/09/2026 00:30"],
    ["2026-12-31T23:59:59Z", "01/01/2027 06:59"],
    ["2026-09-26T14:30:00+07:00", "26/09/2026 14:30"],
  ])("%s → %s", (instant, expected) => {
    expect(formatDateTime(instant)).toBe(expected);
  });

  it("giữ nguyên chuỗi không đọc được", () => {
    expect(formatDateTime("không phải thời điểm")).toBe("không phải thời điểm");
  });
});

describe("parseDecimalInput", () => {
  it.each([
    ["1,5", 1.5],
    ["1.5", 1.5],
    ["12", 12],
    ["0,25", 0.25],
    ["0.125", 0.125],
    [" 2,5 ", 2.5],
    ["-3,5", -3.5],
    // Dấu chấm là dấu thập phân, không phải dấu hàng nghìn.
    ["1.000", 1],
  ])("đọc %j thành %d", (text, expected) => {
    expect(parseDecimalInput(text)).toBe(expected);
  });

  it.each(["", "   ", "abc", "1,2,3", "1.000,5", "1.000.000", "1 000", ",5", "5,", "1e3", "+1"])(
    "trả null với %j",
    (text) => {
      expect(parseDecimalInput(text)).toBeNull();
    },
  );
});
