import { describe, expect, it } from "vitest";
import {
  caretAfterMoneyChars,
  countMoneyCharsBefore,
  formatMoneyText,
  normalizeMoneyText,
  resolveMoneyEdit,
} from "@/lib/money-input";

describe("normalizeMoneyText", () => {
  it.each([
    ["9.000.000", "9000000"],
    ["9000000", "9000000"],
    ["1.500,5", "1500,5"],
    ["1,5,6", "1,56"],
    ["12a3 ₫", "123"],
    ["0009", "9"],
    ["0", "0"],
    ["0,5", "0,5"],
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

describe("resolveMoneyEdit", () => {
  const typed = (data: string) => ({ inputType: "insertText", data });

  it("gõ thêm chữ số: giữ con trỏ sau chữ số vừa gõ", () => {
    // "9.000|" gõ "0" → "9.0000" → giá trị "90000", con trỏ sau 5 chữ số.
    expect(resolveMoneyEdit("9000", "9.0000", 6, typed("0"))).toEqual({ value: "90000", caretChars: 5 });
  });

  it("Backspace ngay sau dấu chấm: xóa luôn chữ số đứng trước dấu chấm", () => {
    const backspace = { inputType: "deleteContentBackward" };
    // "12.|345" bấm Backspace → trình duyệt đưa "12345", con trỏ 2.
    expect(resolveMoneyEdit("12345", "12345", 2, backspace)).toEqual({ value: "1345", caretChars: 1 });
    // "9.|000": xóa "9", ba số 0 còn lại gộp thành "0".
    expect(resolveMoneyEdit("9000", "9000", 1, backspace)).toEqual({ value: "0", caretChars: 0 });
  });

  it("Backspace một chữ số: xóa bình thường", () => {
    expect(resolveMoneyEdit("12345", "12.45", 3, { inputType: "deleteContentBackward" })).toEqual({
      value: "1245",
      caretChars: 2,
    });
  });

  it("Delete ngay trước dấu chấm: xóa luôn chữ số đứng sau dấu chấm", () => {
    // "12|.345" bấm Delete → "12345", con trỏ 2.
    expect(resolveMoneyEdit("12345", "12345", 2, { inputType: "deleteContentForward" })).toEqual({
      value: "1245",
      caretChars: 2,
    });
  });

  it("gõ dấu chấm: hiểu là dấu thập phân", () => {
    expect(resolveMoneyEdit("1500", "1.500.", 6, typed("."))).toEqual({ value: "1500,", caretChars: 5 });
  });

  it("đã có dấu phẩy thì dấu chấm vừa gõ bị bỏ", () => {
    expect(resolveMoneyEdit("1500,5", "1.500,5.", 8, typed("."))).toEqual({ value: "1500,5", caretChars: 6 });
  });

  it("dán chữ có dấu chấm ngăn cách: dấu chấm vẫn là ngăn cách hàng nghìn", () => {
    expect(resolveMoneyEdit("", "9.000.000", 9, { inputType: "insertFromPaste", data: null })).toEqual({
      value: "9000000",
      caretChars: 7,
    });
  });
});
