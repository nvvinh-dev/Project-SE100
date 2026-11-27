// Ô tiền giữ trong form chuỗi số không có dấu ngăn cách ("9000000", "1500,5") để schema đọc bằng parseDecimalInput;
// chỉ phần hiển thị có dấu chấm ngăn cách hàng nghìn ("9.000.000").

/**
 * Chữ người dùng gõ vào ô tiền → chuỗi lưu trong form. Dấu chấm là dấu ngăn cách hàng nghìn nên bỏ đi;
 * dấu phẩy là dấu thập phân, chỉ giữ dấu đầu tiên; ký tự khác bỏ đi. Số 0 thừa ở đầu bỏ đi (`0009` → `9`),
 * để chữ hiện trên ô không thành `0.009`.
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
  return result.replace(/^0+(?=\d)/, "");
}

export interface MoneyEdit {
  /** Chuỗi lưu trong form sau lần gõ. */
  value: string;
  /** Số chữ số và dấu phẩy đứng trước con trỏ, để đặt lại con trỏ sau khi vẽ lại. */
  caretChars: number;
}

/** Thông tin của một lần gõ, lấy từ `InputEvent`: loại thao tác và ký tự vừa gõ. */
export interface MoneyInputEventInfo {
  inputType?: string;
  data?: string | null;
}

/**
 * Một lần gõ vào ô tiền: `value` là chuỗi đang lưu trong form, `text` và `caret` là chữ, con trỏ trình duyệt đưa về.
 * Chỉ dựa vào sự kiện gõ và giá trị trong form, không so với chữ đang hiện, vì khi gõ nhanh chữ đó có thể chưa kịp vẽ lại.
 */
export function resolveMoneyEdit(value: string, text: string, caret: number, event: MoneyInputEventInfo = {}): MoneyEdit {
  let nextText = text;
  let nextCaret = caret;

  if (event.inputType === "insertText" && event.data === "." && caret > 0 && text[caret - 1] === ".") {
    const without = text.slice(0, caret - 1) + text.slice(caret);
    if (!normalizeMoneyText(without).includes(",")) {
      // Bàn phím số của nhiều điện thoại chỉ có phím ".", nên dấu chấm vừa gõ được hiểu là dấu thập phân.
      nextText = text.slice(0, caret - 1) + "," + text.slice(caret);
    }
  }

  const deleting = event.inputType === "deleteContentBackward" || event.inputType === "deleteContentForward";
  if (deleting && normalizeMoneyText(text) === value) {
    // Xóa mà giá trị không đổi nghĩa là vừa xóa trúng dấu chấm ngăn cách; ô nhập sẽ trả lại chữ cũ,
    // nên xóa luôn chữ số cạnh nó theo hướng xóa.
    if (event.inputType === "deleteContentForward") {
      nextText = text.slice(0, caret) + text.slice(caret + 1);
    } else if (caret > 0) {
      nextText = text.slice(0, caret - 1) + text.slice(caret);
      nextCaret = caret - 1;
    }
  }

  return { value: normalizeMoneyText(nextText), caretChars: countMoneyCharsBefore(nextText, nextCaret) };
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
