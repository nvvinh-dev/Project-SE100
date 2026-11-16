import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useMembers } from "@/features/households/queries";
import type { MemberListItem, MemberQuery } from "@/features/households/types";
import { userRoleLabels } from "@/lib/enum-labels";
import type { UserRole } from "@/lib/enums";

// Vai trò có trong một hộ; Quản trị hệ thống không thuộc hộ nào.
const householdRoles = ["Owner", "Member", "Worker"] as const satisfies readonly UserRole[];

// Trạng thái tài khoản trên đường dẫn: "locked" là đã khóa, "active" là đang hoạt động.
const lockStates = ["active", "locked"] as const;
type LockState = (typeof lockStates)[number];
const lockStateLabels: Record<LockState, string> = { active: "Đang hoạt động", locked: "Đã khóa" };

/** `/members`: danh sách người trong hộ (mẫu trang danh sách), chỉ Chủ hộ. Bộ lọc nằm trên đường dẫn để quay lại vẫn giữ. */
export function MemberListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const lockState = pick(searchParams.get("status"), lockStates);
  const query = readQuery(searchParams, lockState);
  const members = useMembers(query);

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
  const createButton = (
    <Link to="/members/new" className={buttonVariants()}>
      <PlusIcon data-icon="inline-start" />
      Thêm thành viên
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Thành viên" action={createButton} />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo tên, số điện thoại"
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

      {members.isPending ? (
        <LoadingState variant="list" />
      ) : members.isError ? (
        <ErrorAlert error={members.error} onRetry={() => void members.refetch()} />
      ) : members.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có thành viên nào." action={createButton} />
        )
      ) : (
        <>
          <DataList
            items={members.data.items}
            getKey={(member) => member.id}
            getHref={(member) => `/members/${member.id}`}
            title={(member) => member.fullName}
            subtitle={memberSummary}
            badge={(member) => member.isLocked && <StatusBadge kind="Locked" />}
            columns={[
              { header: "Họ tên", cell: (member) => member.fullName },
              { header: "Số điện thoại", cell: (member) => member.phone ?? "" },
              { header: "Vai trò", cell: (member) => userRoleLabels[member.role] },
              { header: "Trạng thái", cell: (member) => member.isLocked && <StatusBadge kind="Locked" /> },
            ]}
          />
          <Pagination meta={members.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: vai trò · số điện thoại.
function memberSummary(member: MemberListItem): string {
  return [userRoleLabels[member.role], member.phone].filter(Boolean).join(" · ");
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams, lockState: LockState | undefined): MemberQuery {
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
