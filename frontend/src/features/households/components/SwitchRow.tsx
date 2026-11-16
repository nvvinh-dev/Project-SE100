import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

interface SwitchRowProps {
  id: string;
  label: string;
  /** Một câu nói rõ bật công tắc thì điều gì xảy ra. */
  description: ReactNode;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  disabled?: boolean;
}

/**
 * Một dòng công tắc bật, tắt: nhãn và câu giải thích bên trái, công tắc bên phải. Dùng ô chọn gốc của trình duyệt với
 * `role="switch"` nên bàn phím, trình đọc màn hình hiểu đúng; cả dòng bấm được, cao tối thiểu 44px.
 */
export function SwitchRow({ id, label, description, checked, onCheckedChange, disabled = false }: SwitchRowProps) {
  return (
    <label
      htmlFor={id}
      className={cn(
        "flex min-h-11 cursor-pointer items-start justify-between gap-4 py-3",
        disabled && "cursor-not-allowed opacity-60",
      )}
    >
      <span className="flex flex-col gap-0.5">
        <span className="font-medium">{label}</span>
        <span className="text-sm text-muted-foreground">{description}</span>
      </span>
      <input
        id={id}
        type="checkbox"
        role="switch"
        checked={checked}
        disabled={disabled}
        onChange={(event) => onCheckedChange(event.target.checked)}
        className={cn(
          "relative mt-0.5 h-6 w-11 shrink-0 cursor-pointer appearance-none rounded-full bg-input transition-colors",
          "before:absolute before:top-0.5 before:left-0.5 before:size-5 before:rounded-full before:bg-background",
          "before:shadow-sm before:transition-transform before:content-['']",
          "checked:bg-primary checked:before:translate-x-5",
          "focus-visible:ring-3 focus-visible:ring-ring/50 focus-visible:outline-none disabled:cursor-not-allowed",
        )}
      />
    </label>
  );
}
