import { describe, expect, it } from "vitest";
import { normalizePhone } from "@/lib/phone";

describe("normalizePhone", () => {
  it.each([
    ["0912345678", "0912345678"],
    [" 0912 345 678 ", "0912345678"],
    ["0912.345.678", "0912345678"],
    ["0912-345-678", "0912345678"],
    ["+84912345678", "0912345678"],
    ["+84 912 345 678", "0912345678"],
    ["84912345678", "0912345678"],
  ])("chuẩn hóa %j thành %j", (input, expected) => {
    expect(normalizePhone(input)).toBe(expected);
  });

  it.each([
    "",
    "   ",
    "091234567",
    "09123456789",
    "1912345678",
    "8491234567",
    "+8491234567",
    "0912a45678",
    "(091) 234 5678",
    "０９１２３４５６７８",
  ])("từ chối %j", (input) => {
    expect(normalizePhone(input)).toBeNull();
  });
});
