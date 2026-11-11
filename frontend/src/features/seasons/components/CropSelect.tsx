import { useState } from "react";
import { Link } from "react-router";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useActiveCrops } from "@/features/seasons/queries";
import type { CropOptionItem } from "@/features/seasons/types";
import { unitLabels } from "@/lib/enum-labels";

interface CropSelectProps {
  id: string;
  value: string;
  onChange: (cropId: string, crop?: CropOptionItem) => void;
  disabled?: boolean;
  invalid?: boolean;
  selectedCropFallback?: { id: string; name: string; yieldUnit: string };
}

/** Ô chọn cây trồng (FR-SEA-01, FR-SEA-02): chỉ chọn cây Active, hướng dẫn tạo khi rỗng. */
export function CropSelect({
  id,
  value,
  onChange,
  disabled = false,
  invalid = false,
  selectedCropFallback,
}: CropSelectProps) {
  const [search, setSearch] = useState("");
  const cropsQuery = useActiveCrops();
  const allCrops = cropsQuery.data ?? [];
  const filteredCrops = search.trim()
    ? allCrops.filter((crop) => crop.name.toLowerCase().includes(search.trim().toLowerCase()))
    : allCrops;
  const showFallback =
    selectedCropFallback !== undefined &&
    selectedCropFallback.id === value &&
    !allCrops.some((crop) => crop.id === value);
  const isEmpty = !cropsQuery.isPending && allCrops.length === 0 && !showFallback;

  function handleChange(cropId: string) {
    const found = allCrops.find((c) => c.id === cropId);
    onChange(cropId, found);
  }

  return (
    <div className="flex flex-col gap-2">
      {!disabled && allCrops.length > 5 && (
        <Input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Tìm cây trồng theo tên..."
          aria-label="Tìm cây trồng"
        />
      )}
      <NativeSelect
        id={id}
        className="w-full"
        value={value}
        onChange={(event) => handleChange(event.target.value)}
        disabled={disabled}
        required
        aria-invalid={invalid}
      >
        <NativeSelectOption value="" disabled>
          Chọn cây trồng
        </NativeSelectOption>
        {showFallback && (
          <NativeSelectOption value={selectedCropFallback.id}>
            {selectedCropFallback.name}
          </NativeSelectOption>
        )}
        {filteredCrops.map((crop) => (
          <NativeSelectOption key={crop.id} value={crop.id}>
            {crop.name} ({unitLabels[crop.yieldUnit] ?? crop.yieldUnit})
          </NativeSelectOption>
        ))}
      </NativeSelect>
      {isEmpty && (
        <p className="text-sm text-muted-foreground">
          Chưa có cây trồng đang sử dụng.{" "}
          <Link to="/crops/new" className="text-primary underline hover:text-primary/80">
            Thêm cây trồng mới
          </Link>
        </p>
      )}
    </div>
  );
}
