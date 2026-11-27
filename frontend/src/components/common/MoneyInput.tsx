import { type ChangeEvent, type ComponentProps, useCallback, useLayoutEffect, useRef } from "react";
import { Input } from "@/components/ui/input";
import { caretAfterMoneyChars, formatMoneyText, resolveMoneyEdit } from "@/lib/money-input";

type MoneyInputProps = Omit<ComponentProps<"input">, "value" | "onChange" | "type"> & {
  /** Chuỗi số không có dấu ngăn cách, như schema của form đọc bằng `parseDecimalInput`: `"9000000"`, `"1500,5"`. */
  value: string;
  onChange: (value: string) => void;
};

/**
 * Ô tiền hiện dấu chấm ngăn cách hàng nghìn ngay khi gõ (`9.000.000`) để không gõ thừa, thiếu số 0;
 * giá trị trong form vẫn là chuỗi số không có dấu chấm. Đơn vị ₫ hiện ngay sau ô.
 */
export function MoneyInput({ value, onChange, className, ref, ...props }: MoneyInputProps) {
  const inputRef = useRef<HTMLInputElement>(null);
  // Thêm, bớt dấu chấm làm con trỏ nhảy về cuối; nhớ số chữ số đứng trước con trỏ để đặt lại sau khi vẽ.
  const pendingCaret = useRef<number | null>(null);
  const display = formatMoneyText(value);

  // Giữ cả ref của form (React Hook Form cần nó để focus vào ô lỗi khi gửi) lẫn ref riêng để đặt con trỏ.
  const setRefs = useCallback(
    (node: HTMLInputElement | null) => {
      inputRef.current = node;
      if (typeof ref === "function") {
        ref(node);
      } else if (ref) {
        ref.current = node;
      }
    },
    [ref],
  );

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
    const input = event.target;
    const { inputType, data } = event.nativeEvent as InputEvent;
    const edit = resolveMoneyEdit(value, input.value, input.selectionStart ?? input.value.length, { inputType, data });

    if (edit.value === value) {
      // Giá trị không đổi (gõ chữ cái...) thì React không vẽ lại mà trả ô về chữ cũ, con trỏ nhảy về cuối;
      // đặt lại con trỏ sau khi React trả chữ cũ.
      pendingCaret.current = null;
      const position = caretAfterMoneyChars(display, edit.caretChars);
      requestAnimationFrame(() => {
        if (document.activeElement === input) {
          input.setSelectionRange(position, position);
        }
      });
      return;
    }

    pendingCaret.current = edit.caretChars;
    onChange(edit.value);
  }

  return (
    <div className="flex items-center gap-2">
      <Input
        {...props}
        ref={setRefs}
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
