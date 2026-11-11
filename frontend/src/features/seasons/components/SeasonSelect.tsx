import { useEffect, useMemo, useState } from "react";
import { useSearchParams } from "react-router";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useSeason, useSeasonOptions } from "@/features/seasons/queries";
import type { SeasonOperation, SeasonOptionItem } from "@/features/seasons/types";
import { seasonStatusLabels } from "@/lib/enum-labels";
import { formatDate } from "@/lib/format";

export interface SeasonSelectProps {
  id: string;
  value: string;
  onChange: (seasonId: string, option?: SeasonOptionItem) => void;
  /** Loại thao tác để lọc mùa vụ theo trạng thái (BR-SEA-10) và phạm vi (BR-USR-13). */
  operation: SeasonOperation;
  invalid?: boolean;
  disabled?: boolean;
  required?: boolean;
  placeholder?: string;
  /**
   * Bật nạp chi tiết khi giá trị chọn không nằm trong danh sách lựa chọn hợp lệ.
   * Chỉ nên bật khi sửa bản ghi cũ (mùa vụ đã đóng/hủy sau khi ghi), KHÔNG bật cho form tạo mới
   * để tránh chọn mùa vụ không hợp lệ qua tham số `?seasonId=`.
   */
  allowInactiveFallback?: boolean;
}

/**
 * Ô chọn mùa vụ dùng chung, có ô tìm kiếm phía trên danh sách (FR-SEA-11).
 *
 * Cách dùng cho các trang form (`/tasks/new`, `/stock/out`, `/sales/new`, `/expenses/new`):
 * - Truyền `operation` tương ứng (`ManageFarmTasks`, `StockOut`, `ManageSales`, `ManageExpenses`...)
 *   để backend lọc đúng mùa vụ theo trạng thái (BR-SEA-10) và quyền ngoài phân công (BR-USR-13).
 * - Tự động nhận `?seasonId=` trên đường dẫn khi `value` chưa có và mùa vụ đó hợp lệ.
 * - Khi làm form sửa bản ghi lịch sử cũ (mùa vụ có thể đã đóng), truyền `allowInactiveFallback={true}`
 *   để nạp và hiển thị nhãn dự phòng.
 */
export function SeasonSelect({
  id,
  value,
  onChange,
  operation,
  invalid = false,
  disabled = false,
  required = true,
  placeholder = "Chọn mùa vụ",
  allowInactiveFallback = false,
}: SeasonSelectProps) {
  const [search, setSearch] = useState("");
  const [searchParams] = useSearchParams();
  const querySeasonId = searchParams.get("seasonId");

  const seasonOptions = useSeasonOptions({ operation });
  const allOptions = useMemo(() => seasonOptions.data ?? [], [seasonOptions.data]);

  // Tự động nhận giá trị mặc định từ ?seasonId= khi form chưa có giá trị
  // và mùa vụ đó thuộc danh sách lựa chọn hợp lệ của thao tác hiện tại
  useEffect(() => {
    if (!value && querySeasonId && allOptions.length > 0) {
      const matchingOption = allOptions.find((opt) => opt.id === querySeasonId);
      if (matchingOption) {
        onChange(matchingOption.id, matchingOption);
      }
    }
  }, [value, querySeasonId, allOptions, onChange]);

  // Lọc theo từ khóa tìm kiếm trên nhãn, tên cây, mã thửa
  const term = search.trim().toLowerCase();
  const filteredOptions = term
    ? allOptions.filter(
        (opt) =>
          opt.label.toLowerCase().includes(term) ||
          opt.cropName.toLowerCase().includes(term) ||
          opt.plotCode.toLowerCase().includes(term),
      )
    : allOptions;

  // Mùa vụ đang chọn có thể không nằm trong kết quả tìm, vẫn phải hiện trong ô
  const isSelectedInFiltered = filteredOptions.some((opt) => opt.id === value);
  const selectedFromAll = allOptions.find((opt) => opt.id === value);

  // Chỉ gọi API chi tiết khi allowInactiveFallback được bật (ví dụ khi sửa bản ghi cũ)
  const shouldFetchFallback = allowInactiveFallback && !seasonOptions.isPending && !selectedFromAll && Boolean(value);
  const singleSeason = useSeason(shouldFetchFallback ? value : "");

  const fallbackLabel = selectedFromAll
    ? selectedFromAll.label
    : singleSeason.data
      ? `${singleSeason.data.cropName} · ${singleSeason.data.plotCode} · ${formatDate(singleSeason.data.startDate)} (${seasonStatusLabels[singleSeason.data.status]})`
      : undefined;

  return (
    <div className="flex flex-col gap-2">
      <Input
        type="search"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Tìm theo cây, thửa, ngày"
        aria-label="Tìm mùa vụ"
        disabled={disabled}
      />
      <NativeSelect
        id={id}
        className="w-full"
        value={value}
        onChange={(event) => {
          const selectedId = event.target.value;
          const selectedOption = allOptions.find((opt) => opt.id === selectedId);
          onChange(selectedId, selectedOption);
        }}
        required={required}
        disabled={disabled || seasonOptions.isPending}
        aria-invalid={invalid}
      >
        <NativeSelectOption value="" disabled>
          {seasonOptions.isPending
            ? "Đang tải danh sách mùa vụ..."
            : seasonOptions.isError
              ? "Lỗi tải danh sách mùa vụ"
              : placeholder}
        </NativeSelectOption>
        {!isSelectedInFiltered && value && fallbackLabel && (
          <NativeSelectOption value={value}>{fallbackLabel}</NativeSelectOption>
        )}
        {filteredOptions.map((opt) => (
          <NativeSelectOption key={opt.id} value={opt.id}>
            {opt.label}
          </NativeSelectOption>
        ))}
      </NativeSelect>

      {seasonOptions.isError && (
        <div className="flex items-center justify-between text-xs text-destructive">
          <span>Không thể tải danh sách mùa vụ.</span>
          <Button
            type="button"
            variant="link"
            size="sm"
            className="h-auto p-0 text-xs text-destructive underline hover:text-destructive/80"
            onClick={() => void seasonOptions.refetch()}
          >
            Thử lại
          </Button>
        </div>
      )}
    </div>
  );
}
