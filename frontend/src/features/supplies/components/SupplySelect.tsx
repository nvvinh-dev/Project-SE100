import { useState } from "react";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useSupplies, useSupply } from "@/features/supplies/queries";
import { unitLabels } from "@/lib/enum-labels";
import type { RecordStatus } from "@/lib/enums";

interface SupplySelectProps {
  id: string;
  value: string;
  onChange: (supplyId: string) => void;
  /** Chỉ liệt kê vật tư ở trạng thái này: nhập kho chỉ `Active`; điều chỉnh để trống vì vật tư ngừng dùng vẫn điều chỉnh được. */
  status?: RecordStatus;
  invalid?: boolean;
}

// Mỗi lần chỉ tải một trang; danh sách dài thì người dùng gõ để tìm.
const PAGE_SIZE = 100;

/** Ô chọn vật tư, có ô tìm kiếm phía trên danh sách (mẫu trang form). */
export function SupplySelect({ id, value, onChange, status, invalid = false }: SupplySelectProps) {
  const [search, setSearch] = useState("");
  const supplies = useSupplies({ search: search.trim() || undefined, status, page: 1, pageSize: PAGE_SIZE });
  const selected = useSupply(value);
  const items = supplies.data?.items ?? [];
  // Vật tư đang chọn có thể không nằm trong kết quả tìm, vẫn phải hiện trong ô.
  const showSelected = selected.data !== undefined && !items.some((supply) => supply.id === value);

  return (
    <div className="flex flex-col gap-2">
      <Input
        type="search"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Tìm theo mã, tên"
        aria-label="Tìm vật tư"
      />
      <NativeSelect
        id={id}
        className="w-full"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        required
        aria-invalid={invalid}
      >
        <NativeSelectOption value="" disabled>
          Chọn vật tư
        </NativeSelectOption>
        {showSelected && (
          <NativeSelectOption value={selected.data.id}>
            {`${selected.data.name} (${selected.data.code})`}
          </NativeSelectOption>
        )}
        {items.map((supply) => (
          <NativeSelectOption key={supply.id} value={supply.id}>
            {`${supply.name} (${supply.code}, ${unitLabels[supply.unit]})`}
          </NativeSelectOption>
        ))}
      </NativeSelect>
    </div>
  );
}
