import { type ChangeEvent, type ComponentProps, useLayoutEffect, useRef } from "react";
import { Input } from "@/components/ui/input";
import { caretAfterMoneyChars, countMoneyCharsBefore, formatMoneyText, normalizeMoneyText } from "@/lib/money-input";

type MoneyInputProps = Omit<ComponentProps<"input">, "value" | "onChange" | "type"> & {
  /** Chuỗi số không có dấu ngăn cách, như schema của form đọc bằng `parseDecimalInput`: `"9000000"`, `"1500,5"`. */
  value: string;
  onChange: (value: string) => void;
};

/**
 * Ô tiền hiện dấu chấm ngăn cách hàng nghìn ngay khi gõ (`9.000.000`) để không gõ thừa, thiếu số 0;
 * giá trị trong form vẫn là chuỗi số không có dấu chấm. Đơn vị ₫ hiện ngay sau ô.
 */
export function MoneyInput({ value, onChange, className, ...props }: MoneyInputProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  // Thêm, bớt dấu chấm làm con trỏ nhảy về cuối; nhớ số chữ số đứng trước con trỏ để đặt lại sau khi vẽ.
  const pendingCaret = useRef<number | null>(null);
  const display = formatMoneyText(value);

  useLayoutEffect(() => {
    const input = inputRef.current;
    if (pendingCaret.current === null || !input || document.activeElement !== input) {
      return;
    }
    const position = caretAfterMoneyChars(input.value, pendingCaret.current);
    input.setSelectionRange(position, position);
    pendingCaret.current = null;
  }, [display]);

  function handleChange(event: ChangeEvent<HTMLInputElement>) {
    const { value: text, selectionStart } = event.target;
    pendingCaret.current = countMoneyCharsBefore(text, selectionStart ?? text.length);
    onChange(normalizeMoneyText(text));
  }

  return (
    <div className="flex items-center gap-2">
      <Input
        {...props}
        ref={inputRef}
        type="text"
        inputMode="decimal"
        autoComplete="off"
        value={display}
        onChange={handleChange}
        className={className}
      />
      <span className="shrink-0 text-sm text-muted-foreground">₫</span>
    </div>
  );
}
