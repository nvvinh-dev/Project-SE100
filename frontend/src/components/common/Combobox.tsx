import { ChevronsUpDownIcon, Loader2Icon, PlusIcon } from "lucide-react";
import { type ReactNode, useState } from "react";
import { Button } from "@/components/ui/button";
import { Command, CommandEmpty, CommandGroup, CommandInput, CommandItem, CommandList } from "@/components/ui/command";
import { Popover, PopoverContent, PopoverTrigger } from "@/components/ui/popover";
import { cn } from "@/lib/utils";

export interface ComboboxOption {
  value: string;
  label: string;
  /** Dòng phụ màu xám dưới nhãn, ví dụ mã vật tư, tồn hiện tại. */
  description?: string;
}

export interface ComboboxGroup {
  /** Tiêu đề nhóm, ví dụ "Trong kế hoạch mùa vụ"; để trống thì không hiện tiêu đề. */
  heading?: string;
  options: readonly ComboboxOption[];
}

export interface ComboboxCreateAction {
  /** Chữ trên dòng tạo mới, luôn có tên đối tượng: "Thêm vật tư". */
  label: string;
  onSelect: () => void;
}

interface ComboboxProps {
  id?: string;
  /** Giá trị đang chọn; `""` là chưa chọn. */
  value: string;
  onChange: (value: string) => void;
  /** Danh sách một nhóm, hoặc nhiều nhóm theo thứ tự hiển thị. */
  options: readonly ComboboxOption[] | readonly ComboboxGroup[];
  /** Nhãn của giá trị đang chọn khi nó không có trong `options` (danh sách tải theo chữ gõ, giá trị từ đường dẫn). */
  selectedLabel?: string;
  placeholder?: string;
  searchPlaceholder?: string;
  /** Câu khi không có mục nào khớp, ví dụ "Chưa có vật tư nào." */
  emptyText?: string;
  /** Có thì backend lọc theo chữ gõ (danh sách dài, phân trang); không có thì lọc ngay trên `options`. */
  onSearchChange?: (search: string) => void;
  loading?: boolean;
  /** Dòng tạo mới ở cuối danh sách, kể cả khi danh sách rỗng. */
  createAction?: ComboboxCreateAction;
  disabled?: boolean;
  invalid?: boolean;
  className?: string;
  /** Nội dung thêm ở cuối danh sách, ví dụ nút "Xem thêm". */
  footer?: ReactNode;
}

function toGroups(options: ComboboxProps["options"]): readonly ComboboxGroup[] {
  if (options.length === 0) {
    return [];
  }
  return "options" in options[0] ? (options as readonly ComboboxGroup[]) : [{ options: options as readonly ComboboxOption[] }];
}

/**
 * Ô chọn từ danh sách dài: một ô duy nhất, bấm vào mở danh sách, gõ ngay trong đó để lọc.
 * Không tách ô tìm và ô chọn thành hai control, để trên điện thoại người dùng biết gõ vào đâu.
 */
export function Combobox({
  id,
  value,
  onChange,
  options,
  selectedLabel,
  placeholder = "Chọn",
  searchPlaceholder = "Gõ để tìm",
  emptyText = "Không có kết quả phù hợp.",
  onSearchChange,
  loading = false,
  createAction,
  disabled = false,
  invalid = false,
  className,
  footer,
}: ComboboxProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const groups = toGroups(options);
  const selected = groups.flatMap((group) => group.options).find((option) => option.value === value);
  const triggerLabel = selected?.label ?? (value ? selectedLabel : undefined);

  function handleOpenChange(next: boolean) {
    setOpen(next);
    if (!next && search) {
      setSearch("");
      onSearchChange?.("");
    }
  }

  function handleSearchChange(text: string) {
    setSearch(text);
    onSearchChange?.(text);
  }

  function choose(nextValue: string) {
    onChange(nextValue);
    handleOpenChange(false);
  }

  return (
    <Popover open={open} onOpenChange={handleOpenChange}>
      <PopoverTrigger asChild>
        <Button
          id={id}
          type="button"
          variant="outline"
          role="combobox"
          aria-expanded={open}
          aria-invalid={invalid}
          disabled={disabled}
          className={cn(
            "h-11 w-full justify-between px-2.5 font-normal md:h-8 aria-invalid:border-destructive aria-invalid:ring-3 aria-invalid:ring-destructive/20",
            className,
          )}
        >
          <span className={cn("truncate", !triggerLabel && "text-muted-foreground")}>{triggerLabel ?? placeholder}</span>
          <ChevronsUpDownIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
        </Button>
      </PopoverTrigger>
      <PopoverContent align="start" className="w-(--radix-popover-trigger-width) min-w-64 p-0">
        {/* Backend đã lọc thì tắt bộ lọc của cmdk, để không lọc lần hai mất các mục khớp theo mã. */}
        <Command shouldFilter={!onSearchChange}>
          <CommandInput value={search} onValueChange={handleSearchChange} placeholder={searchPlaceholder} />
          <CommandList>
            {loading ? (
              <div className="flex items-center gap-2 px-3 py-4 text-sm text-muted-foreground">
                <Loader2Icon className="size-4 animate-spin" aria-hidden />
                Đang tải...
              </div>
            ) : (
              <>
                <CommandEmpty>{emptyText}</CommandEmpty>
                {groups.map((group, index) => (
                  <CommandGroup key={group.heading ?? index} heading={group.heading}>
                    {group.options.map((option) => (
                      <CommandItem
                        key={option.value}
                        // cmdk lọc theo `value`; ghép nhãn và dòng phụ để gõ mã hay tên đều ra.
                        value={`${option.label} ${option.description ?? ""} ${option.value}`}
                        data-checked={option.value === value}
                        onSelect={() => choose(option.value)}
                        className="min-h-11 md:min-h-8"
                      >
                        <span className="flex min-w-0 flex-col">
                          <span className="truncate">{option.label}</span>
                          {option.description && (
                            <span className="truncate text-xs text-muted-foreground">{option.description}</span>
                          )}
                        </span>
                      </CommandItem>
                    ))}
                  </CommandGroup>
                ))}
              </>
            )}
            {createAction && (
              <CommandGroup forceMount>
                <CommandItem
                  forceMount
                  value={`__create__ ${createAction.label}`}
                  onSelect={() => {
                    handleOpenChange(false);
                    createAction.onSelect();
                  }}
                  className="min-h-11 text-primary md:min-h-8"
                >
                  <PlusIcon aria-hidden />
                  {createAction.label}
                </CommandItem>
              </CommandGroup>
            )}
            {footer}
          </CommandList>
        </Command>
      </PopoverContent>
    </Popover>
  );
}
