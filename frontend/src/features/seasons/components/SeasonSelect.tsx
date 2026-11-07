import { useState } from "react";
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
}

/** Ô chọn mùa vụ dùng chung, có ô tìm kiếm phía trên danh sách (FR-SEA-11). */
export function SeasonSelect({
  id,
  value,
  onChange,
  operation,
  invalid = false,
  disabled = false,
  required = true,
  placeholder = "Chọn mùa vụ",
}: SeasonSelectProps) {
  const [search, setSearch] = useState("");
  const seasonOptions = useSeasonOptions({ operation });
  const allOptions = seasonOptions.data ?? [];

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
  // Chỉ gọi API chi tiết khi mùa vụ đang chọn không nằm trong danh sách lựa chọn (ví dụ khi sửa bản ghi cũ)
  const fallbackSeasonId = !seasonOptions.isPending && !selectedFromAll ? value : "";
  const singleSeason = useSeason(fallbackSeasonId);

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
          {seasonOptions.isPending ? "Đang tải danh sách mùa vụ..." : placeholder}
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
    </div>
  );
}
