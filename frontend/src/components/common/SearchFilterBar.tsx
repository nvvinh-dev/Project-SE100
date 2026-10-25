import { ListFilterIcon, SearchIcon, XIcon } from "lucide-react";
import { type ReactNode, useEffect, useEffectEvent, useState, useSyncExternalStore } from "react";
import { Button } from "@/components/ui/button";
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";

export interface ActiveFilter {
  key: string;
  /** Tên và giá trị bộ lọc, ví dụ "Trạng thái: Đang canh tác". */
  label: string;
  onRemove: () => void;
}

interface SearchFilterBarProps {
  search: string;
  /** Gọi khi người dùng ngừng gõ hoặc bấm Enter, với từ khóa đã bỏ khoảng trắng hai đầu. */
  onSearchChange: (search: string) => void;
  searchPlaceholder?: string;
  /** Các ô lọc của trang: điện thoại hiện trong hộp thoại "Lọc", máy tính hiện ngay cạnh ô tìm. */
  filters?: ReactNode;
  /** Bộ lọc đang áp dụng; trên điện thoại hiện thành nhãn nhỏ có nút bỏ. */
  activeFilters?: readonly ActiveFilter[];
}

const SEARCH_DELAY_MS = 400;
const DESKTOP_QUERY = "(min-width: 768px)";

function subscribeDesktop(onChange: () => void) {
  const query = window.matchMedia(DESKTOP_QUERY);
  query.addEventListener("change", onChange);
  return () => query.removeEventListener("change", onChange);
}

// Ô lọc chỉ được dựng ở một chỗ (hộp thoại hoặc ngay trên thanh), để không có hai ô cùng id trên trang.
function useIsDesktop() {
  return useSyncExternalStore(subscribeDesktop, () => window.matchMedia(DESKTOP_QUERY).matches);
}

export function SearchFilterBar({
  search,
  onSearchChange,
  searchPlaceholder = "Tìm kiếm",
  filters,
  activeFilters = [],
}: SearchFilterBarProps) {
  const isDesktop = useIsDesktop();
  const [filterOpen, setFilterOpen] = useState(false);
  const [text, setText] = useState(search);
  const [shownSearch, setShownSearch] = useState(search);

  // Từ khóa đổi từ bên ngoài (bấm "Bỏ lọc", quay lại trang trước) thì ô tìm hiện theo.
  if (search !== shownSearch) {
    setShownSearch(search);
    setText(search);
  }

  const commitSearch = useEffectEvent((value: string) => onSearchChange(value));

  useEffect(() => {
    const value = text.trim();
    if (value === search) {
      return;
    }
    // Chờ người dùng ngừng gõ mới tìm, để không gọi API sau từng chữ.
    const timer = setTimeout(() => commitSearch(value), SEARCH_DELAY_MS);
    return () => clearTimeout(timer);
  }, [text, search]);

  const filterCount = activeFilters.length;

  return (
    <div className="flex flex-col gap-2">
      <div className="flex flex-wrap items-center gap-2">
        <form
          role="search"
          className="relative min-w-0 flex-1 md:w-xs md:flex-none"
          onSubmit={(event) => {
            event.preventDefault();
            onSearchChange(text.trim());
          }}
        >
          <SearchIcon className="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" />
          <Input
            type="search"
            value={text}
            onChange={(event) => setText(event.target.value)}
            placeholder={searchPlaceholder}
            aria-label={searchPlaceholder}
            className="pl-8"
          />
        </form>
        {filters &&
          (isDesktop ? (
            <div className="flex flex-wrap items-center gap-2">{filters}</div>
          ) : (
            <Button variant="outline" onClick={() => setFilterOpen(true)}>
              <ListFilterIcon data-icon="inline-start" />
              {filterCount > 0 ? `Lọc (${filterCount})` : "Lọc"}
            </Button>
          ))}
      </div>

      {!isDesktop && filterCount > 0 && (
        <ul className="flex flex-wrap gap-2" aria-label="Bộ lọc đang áp dụng">
          {activeFilters.map((filter) => (
            <li key={filter.key}>
              <button
                type="button"
                onClick={filter.onRemove}
                aria-label={`Bỏ lọc ${filter.label}`}
                className="inline-flex min-h-11 items-center gap-1.5 rounded-full border bg-muted px-3 text-sm"
              >
                {filter.label}
                <XIcon className="size-4 shrink-0" />
              </button>
            </li>
          ))}
        </ul>
      )}

      {!isDesktop && filters && (
        <Dialog open={filterOpen} onOpenChange={setFilterOpen}>
          <DialogContent showCloseButton={false} aria-describedby={undefined}>
            <DialogHeader>
              <DialogTitle>Lọc</DialogTitle>
            </DialogHeader>
            <div className="flex flex-col gap-4">{filters}</div>
            <DialogFooter>
              <Button onClick={() => setFilterOpen(false)}>Xem kết quả</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}
