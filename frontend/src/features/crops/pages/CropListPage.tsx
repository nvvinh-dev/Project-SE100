import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { PermissionGate } from "@/components/common/PermissionGate";
import { type ActiveFilter, SearchFilterBar } from "@/components/common/SearchFilterBar";
import { StatusBadge } from "@/components/common/StatusBadge";
import { buttonVariants } from "@/components/ui/button";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { useCrops } from "@/features/crops/queries";
import type { CropListItem, CropQuery } from "@/features/crops/types";
import { ACCESS, canAccess } from "@/lib/auth/access";
import { useCurrentUser } from "@/lib/auth/use-current-user";
import { cropGroupLabels, recordStatusLabels, unitLabels } from "@/lib/enum-labels";
import { type CropGroup, cropGroups, type RecordStatus, recordStatuses } from "@/lib/enums";

/**
 * `/crops`: danh sách cây trồng (mẫu trang danh sách). Bộ lọc, trang hiện tại nằm trên đường dẫn để quay lại vẫn giữ.
 * Không có trang chi tiết cây: Chủ hộ chạm một dòng để mở trang sửa, Thành viên hộ chỉ xem.
 */
export function CropListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const crops = useCrops(query);
  const { data: user } = useCurrentUser();
  const canEdit = user !== undefined && canAccess(user, ACCESS.owner);

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
    query.group && {
      key: "group",
      label: `Nhóm cây: ${cropGroupLabels[query.group]}`,
      onRemove: () => updateParams({ group: undefined }),
    },
    query.status && {
      key: "status",
      label: `Trạng thái: ${recordStatusLabels[query.status]}`,
      onRemove: () => updateParams({ status: undefined }),
    },
  ].filter((filter) => filter !== undefined);

  const isFiltered = activeFilters.length > 0 || Boolean(query.search);
  const createButton = (
    <Link to="/crops/new" className={buttonVariants()}>
      <PlusIcon data-icon="inline-start" />
      Thêm cây trồng
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Cây trồng" action={<PermissionGate allowed={canEdit}>{createButton}</PermissionGate>} />

      <SearchFilterBar
        search={query.search ?? ""}
        onSearchChange={(search) => updateParams({ search: search || undefined })}
        searchPlaceholder="Tìm theo tên cây"
        activeFilters={activeFilters}
        filters={
          <>
            <FilterSelect
              id="filter-group"
              label="Nhóm cây"
              value={query.group}
              options={cropGroups.map((value) => ({ value, label: cropGroupLabels[value] }))}
              onChange={(group) => updateParams({ group })}
            />
            <FilterSelect
              id="filter-status"
              label="Trạng thái"
              value={query.status}
              options={recordStatuses.map((value) => ({ value, label: recordStatusLabels[value] }))}
              onChange={(status) => updateParams({ status })}
            />
          </>
        }
      />

      {crops.isPending ? (
        <LoadingState variant="list" />
      ) : crops.isError ? (
        <ErrorAlert error={crops.error} onRetry={() => void crops.refetch()} />
      ) : crops.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có cây trồng nào." action={canEdit ? createButton : undefined} />
        )
      ) : (
        <>
          <DataList
            items={crops.data.items}
            getKey={(crop) => crop.id}
            getHref={canEdit ? (crop) => `/crops/${crop.id}/edit` : undefined}
            title={(crop) => crop.name}
            subtitle={cropSummary}
            badge={(crop) => crop.status && <StatusBadge kind="RecordStatus" value={crop.status} />}
            columns={[
              { header: "Tên cây", cell: (crop) => crop.name },
              { header: "Nhóm cây", cell: (crop) => (crop.group ? cropGroupLabels[crop.group] : "") },
              { header: "Đơn vị sản lượng", cell: (crop) => (crop.yieldUnit ? unitLabels[crop.yieldUnit] : "") },
              {
                header: "Trạng thái",
                cell: (crop) => crop.status && <StatusBadge kind="RecordStatus" value={crop.status} />,
              },
            ]}
          />
          <Pagination meta={crops.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: nhóm cây · đơn vị sản lượng.
function cropSummary(crop: CropListItem): string {
  return [crop.group ? cropGroupLabels[crop.group] : null, crop.yieldUnit ? unitLabels[crop.yieldUnit] : null]
    .filter(Boolean)
    .join(" · ");
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams): CropQuery {
  const page = Number(params.get("page"));
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    search: params.get("search") ?? undefined,
    group: pick(params.get("group"), cropGroups),
    status: pick(params.get("status"), recordStatuses),
  };
}

function pick<T extends string>(value: string | null, allowed: readonly T[]): T | undefined {
  return allowed.find((item) => item === value);
}

interface FilterSelectProps<T extends CropGroup | RecordStatus> {
  id: string;
  label: string;
  value: T | undefined;
  options: readonly { value: T; label: string }[];
  onChange: (value: T | undefined) => void;
}

function FilterSelect<T extends CropGroup | RecordStatus>({ id, label, value, options, onChange }: FilterSelectProps<T>) {
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
