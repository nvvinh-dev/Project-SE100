// Ô tiền giữ trong form chuỗi số không có dấu ngăn cách ("9000000", "1500,5") để schema đọc bằng parseDecimalInput;
// chỉ phần hiển thị có dấu chấm ngăn cách hàng nghìn ("9.000.000").

/**
 * Chữ người dùng gõ vào ô tiền → chuỗi lưu trong form. Dấu chấm là dấu ngăn cách hàng nghìn nên bỏ đi;
 * dấu phẩy là dấu thập phân, chỉ giữ dấu đầu tiên; ký tự khác bỏ đi.
 */
export function normalizeMoneyText(text: string): string {
  let result = "";
  let hasComma = false;
  for (const char of text) {
    if (char >= "0" && char <= "9") {
      result += char;
    } else if (char === "," && !hasComma) {
      hasComma = true;
      result += char;
    }
  }
  return result;
}

/**
 * Chuỗi lưu trong form → chữ hiện trên ô: `9000000` → `9.000.000`, `1500,5` → `1.500,5`.
 * Giá trị mặc định có thể dùng dấu chấm thập phân (`String(150000.5)`), nên dấu chấm cũng được coi là dấu thập phân.
 */
export function formatMoneyText(raw: string): string {
  const value = raw.replace(".", ",");
  const commaIndex = value.indexOf(",");
  const integerPart = commaIndex === -1 ? value : value.slice(0, commaIndex);
  const decimalPart = commaIndex === -1 ? "" : value.slice(commaIndex);
  const grouped = integerPart.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
  return grouped + decimalPart;
}

/** Số chữ số và dấu phẩy đứng trước vị trí con trỏ, để đặt lại con trỏ sau khi chèn dấu chấm. */
export function countMoneyCharsBefore(text: string, caret: number): number {
  return normalizeMoneyText(text.slice(0, caret)).length;
}

/** Vị trí con trỏ trong chữ đã định dạng, ngay sau ký tự có nghĩa thứ `count` (bỏ qua dấu chấm ngăn cách). */
export function caretAfterMoneyChars(display: string, count: number): number {
  if (count <= 0) {
    return 0;
  }
  let seen = 0;
  for (let index = 0; index < display.length; index++) {
    if (display[index] !== ".") {
      seen++;
    }
    if (seen === count) {
      return index + 1;
    }
  }
  return display.length;
}
