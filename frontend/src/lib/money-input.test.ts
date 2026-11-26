import { describe, expect, it } from "vitest";
import { caretAfterMoneyChars, countMoneyCharsBefore, formatMoneyText, normalizeMoneyText } from "@/lib/money-input";

describe("normalizeMoneyText", () => {
  it.each([
    ["9.000.000", "9000000"],
    ["9000000", "9000000"],
    ["1.500,5", "1500,5"],
    ["1,5,6", "1,56"],
    ["12a3 ₫", "123"],
    ["", ""],
  ])("%s → %s", (text, expected) => {
    expect(normalizeMoneyText(text)).toBe(expected);
  });
});

describe("formatMoneyText", () => {
  it.each([
    ["9000000", "9.000.000"],
    ["900", "900"],
    ["1000", "1.000"],
    ["1500,5", "1.500,5"],
    ["150000.5", "150.000,5"],
    ["1500,", "1.500,"],
    ["", ""],
  ])("%s → %s", (raw, expected) => {
    expect(formatMoneyText(raw)).toBe(expected);
  });
});

describe("vị trí con trỏ", () => {
  it("đếm chữ số trước con trỏ, bỏ dấu chấm", () => {
    expect(countMoneyCharsBefore("9.000.000", 5)).toBe(4);
  });

  it("đặt con trỏ sau đúng chữ số trong chữ mới", () => {
    // Gõ thêm "0" vào cuối "900.000" → "9000000" → "9.000.000": con trỏ ở cuối.
    expect(caretAfterMoneyChars("9.000.000", 7)).toBe(9);
    // Sau chữ số thứ 4 của "9.000.000" là vị trí 5.
    expect(caretAfterMoneyChars("9.000.000", 4)).toBe(5);
    expect(caretAfterMoneyChars("9.000.000", 0)).toBe(0);
  });
});
