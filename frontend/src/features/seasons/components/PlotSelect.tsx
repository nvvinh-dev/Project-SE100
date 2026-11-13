import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { Link } from "react-router";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { getPlot } from "@/features/plots/api";
import { plotKeys, usePlots } from "@/features/plots/queries";

interface PlotSelectProps {
  id: string;
  value: string;
  onChange: (plotId: string) => void;
  disabled?: boolean;
  invalid?: boolean;
}

const PAGE_SIZE = 100;

/** Ô chọn thửa đất (FR-SEA-01, FR-SEA-02): chỉ chọn thửa Active, hướng dẫn tạo khi rỗng. */
export function PlotSelect({ id, value, onChange, disabled = false, invalid = false }: PlotSelectProps) {
  const [search, setSearch] = useState("");
  const plots = usePlots({ search: search.trim() || undefined, status: "Active", page: 1, pageSize: PAGE_SIZE });
  const selected = useQuery({
    queryKey: plotKeys.detail(value),
    queryFn: () => getPlot(value),
    enabled: Boolean(value),
  });
  const items = plots.data?.items ?? [];
  const showSelected =
    Boolean(value) &&
    selected.data !== undefined &&
    selected.data.id === value &&
    !items.some((plot) => plot.id === value);
  const isEmpty = !plots.isPending && items.length === 0 && !search.trim();
  const totalCount = plots.data?.meta.totalCount ?? items.length;
  const showSearch = !disabled && (totalCount > 5 || Boolean(search.trim()));

  return (
    <div className="flex flex-col gap-2">
      {showSearch && (
        <Input
          type="search"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          placeholder="Tìm thửa đất theo mã..."
          aria-label="Tìm thửa đất"
        />
      )}
      <NativeSelect
        id={id}
        className="w-full"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        disabled={disabled}
        required
        aria-invalid={invalid}
      >
        <NativeSelectOption value="" disabled>
          Chọn thửa đất
        </NativeSelectOption>
        {showSelected && (
          <NativeSelectOption value={selected.data.id}>
            {selected.data.code} {selected.data.locationDescription ? `(${selected.data.locationDescription})` : ""}
          </NativeSelectOption>
        )}
        {items.map((plot) => (
          <NativeSelectOption key={plot.id} value={plot.id}>
            {plot.code} {plot.locationDescription ? `(${plot.locationDescription})` : ""}
          </NativeSelectOption>
        ))}
      </NativeSelect>
      {isEmpty && (
        <p className="text-sm text-muted-foreground">
          Chưa có thửa đất đang sử dụng.{" "}
          <Link to="/plots/new" className="text-primary underline hover:text-primary/80">
            Thêm thửa đất mới
          </Link>
        </p>
      )}
    </div>
  );
}
