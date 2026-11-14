import { useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useAdminAccounts } from "@/features/admin/queries";
import type { AdminAccountQuery, AdminAccountResponse } from "@/features/admin/types";
import { userRoleLabels } from "@/lib/enum-labels";
import type { UserRole } from "@/lib/enums";

// Vai trò của tài khoản thuộc hộ; tài khoản Quản trị không có trong danh sách (BR-USR-06).
const householdRoles = ["Owner", "Member", "Worker"] as const satisfies readonly UserRole[];

// Trạng thái trên đường dẫn: "locked" là đã khóa, "active" là đang hoạt động.
const lockStates = ["active", "locked"] as const;
type LockState = (typeof lockStates)[number];
const lockStateLabels: Record<LockState, string> = { active: "Đang hoạt động", locked: "Đã khóa" };

/**
 * `/admin`: danh sách tài khoản của mọi hộ, chỉ Quản trị (FR-ADM-01). Thiết kế cho máy tính, bảng từ màn hình `md`.
 * Chỉ thông tin tài khoản, không có dữ liệu sản xuất, tài chính.
 */
export function AdminAccountListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const lockState = pick(searchParams.get("status"), lockStates);
  const query = readQuery(searchParams, lockState);
  const accounts = useAdminAccounts(query);

  // Đổi bộ lọc, từ khóa thì về trang 1; chỉ giữ tham số có giá trị để đường dẫn gọn.
  function updateParams(changes: Record<string, string | undefined>) {
    setSearchParams((params) => {
      const next = new URLSearchParams(params);
      for (const [key, value] of Object.entries(changes)) {
        if (value) {
          next.set(key, value);
        } else {
          next.delete(key);
        }
      }
      if (!("page" in changes)) {
        next.delete("page");
      }
      return next;
    });
  }

  const activeFilters: ActiveFilter[] = [
    query.role && {
      key: "role",
      label: `Vai trò: ${userRoleLabels[query.role]}`,
      onRemove: () => updateParams({ role: undefined }),
    },
    lockState && {
      key: "status",
      label: `Trạng thái: ${lockStateLabels[lockState]}`,
      onRemove: () => updateParams({ status: undefined }),
    },
  ].filter((filter) => filter !== undefined);

  const isFiltered = activeFilters.length > 0 || Boolean(query.search);

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Tài khoản" description="Tài khoản của mọi hộ, ở mức thông tin tài khoản." />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo tên, số điện thoại, mã hộ, tên hộ"
        activeFilters={activeFilters}
        filters={
          <>
            <FilterSelect
              id="filter-role"
              label="Vai trò"
              value={query.role}
              options={householdRoles.map((value) => ({ value, label: userRoleLabels[value] }))}
              onChange={(role) => updateParams({ role })}
            />
            <FilterSelect
              id="filter-status"
              label="Trạng thái"
              value={lockState}
              options={lockStates.map((value) => ({ value, label: lockStateLabels[value] }))}
              onChange={(status) => updateParams({ status })}
            />
          </>
        }
      />

      {accounts.isPending ? (
        <LoadingState variant="list" />
      ) : accounts.isError ? (
        <ErrorAlert error={accounts.error} onRetry={() => void accounts.refetch()} />
      ) : accounts.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có tài khoản nào." />
        )
      ) : (
        <>
          <DataList
            items={accounts.data.items}
            getKey={(account) => account.id}
            getHref={(account) => `/admin/accounts/${account.id}`}
            title={(account) => account.fullName}
            subtitle={accountSummary}
            badge={(account) => account.isLocked && <StatusBadge kind="Locked" />}
            columns={[
              { header: "Họ tên", cell: (account) => account.fullName },
              { header: "Số điện thoại", cell: (account) => account.phone },
              { header: "Vai trò", cell: (account) => userRoleLabels[account.role] },
              { header: "Hộ", cell: (account) => `${account.householdCode} · ${account.householdName}` },
              { header: "Trạng thái", cell: (account) => account.isLocked && <StatusBadge kind="Locked" /> },
            ]}
          />
          <Pagination meta={accounts.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: vai trò · số điện thoại · mã hộ.
function accountSummary(account: AdminAccountResponse): string {
  return [userRoleLabels[account.role], account.phone, account.householdCode].join(" · ");
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams, lockState: LockState | undefined): AdminAccountQuery {
  const page = Number(params.get("page"));
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    role: pick(params.get("role"), householdRoles),
    isLocked: lockState === undefined ? undefined : lockState === "locked",
  };
}

function pick<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}

interface FilterSelectProps<T extends string> {
  id: string;
  label: string;
  value: T | undefined;
  options: readonly { value: T; label: string }[];
  onChange: (value: T | undefined) => void;
}

function FilterSelect<T extends string>({ id, label, value, options, onChange }: FilterSelectProps<T>) {
  return (
    <div className="flex flex-col gap-1.5 md:flex-row md:items-center md:gap-2">
      <label htmlFor={id} className="text-sm font-medium md:sr-only">
        {label}
      </label>
      <NativeSelect
        id={id}
        className="w-full md:w-auto"
        value={value ?? ""}
        onChange={(event) => onChange(pick(event.target.value, options.map((option) => option.value)))}
      >
        <NativeSelectOption value="">{`${label}: tất cả`}</NativeSelectOption>
        {options.map((option) => (
          <NativeSelectOption key={option.value} value={option.value}>
            {option.label}
          </NativeSelectOption>
        ))}
      </NativeSelect>
    </div>
  );
}
