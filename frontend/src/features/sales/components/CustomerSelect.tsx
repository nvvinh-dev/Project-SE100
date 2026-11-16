import { useState } from "react";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useCustomer, useCustomers } from "@/features/sales/queries";

interface CustomerSelectProps {
  id: string;
  value: string;
  onChange: (customerId: string) => void;
  invalid?: boolean;
  /**
   * Form điều chỉnh: khách hàng đang gắn với giao dịch vẫn hiện trong ô dù đã ngừng sử dụng (BR-GEN-13 cho phép
   * giữ nguyên khách hàng cũ). Form tạo để trống.
   */
  keepCurrent?: string;
}

// Đủ cho danh mục khách hàng của một hộ; muốn tìm sâu hơn thì gõ vào ô tìm.
const PAGE_SIZE = 100;

/** Ô chọn khách hàng có ô tìm theo tên, số điện thoại; chỉ liệt kê khách hàng đang sử dụng (BR-GEN-13). */
export function CustomerSelect({ id, value, onChange, invalid = false, keepCurrent }: CustomerSelectProps) {
  const [search, setSearch] = useState("");
  const customers = useCustomers({ status: "Active", search: search.trim() || undefined, pageSize: PAGE_SIZE });
  const items = customers.data?.items ?? [];
  const current = useCustomer(keepCurrent ?? "");
  const showCurrent = Boolean(keepCurrent) && value === keepCurrent && !items.some((item) => item.id === keepCurrent);

  return (
    <div className="flex flex-col gap-2">
      <Input
        type="search"
        value={search}
        onChange={(event) => setSearch(event.target.value)}
        placeholder="Tìm theo tên, số điện thoại"
        aria-label="Tìm khách hàng"
      />
      <NativeSelect
        id={id}
        className="w-full"
        value={value}
        onChange={(event) => onChange(event.target.value)}
        aria-invalid={invalid}
      >
        <NativeSelectOption value="">{customers.isPending ? "Đang tải khách hàng..." : "Không chọn"}</NativeSelectOption>
        {showCurrent && current.data && (
          <NativeSelectOption value={current.data.id}>
            {current.data.status === "Inactive" ? `${current.data.name} (ngừng sử dụng)` : current.data.name}
          </NativeSelectOption>
        )}
        {items.map((customer) => (
          <NativeSelectOption key={customer.id} value={customer.id}>
            {customer.phone ? `${customer.name} · ${customer.phone}` : customer.name}
          </NativeSelectOption>
        ))}
      </NativeSelect>
    </div>
  );
}
