import { PlusIcon } from "lucide-react";
import { Link, useSearchParams } from "react-router";
import { DataList } from "@/components/common/DataList";
import { DateText } from "@/components/common/DateText";
import { EmptyState } from "@/components/common/EmptyState";
import { ErrorAlert } from "@/components/common/ErrorAlert";
import { LoadingState } from "@/components/common/LoadingState";
import { MoneyText } from "@/components/common/MoneyText";
import { PageHeader } from "@/components/common/PageHeader";
import { Pagination } from "@/components/common/Pagination";
import { StatusBadge } from "@/components/common/StatusBadge";
import { Button, buttonVariants } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { NativeSelect, NativeSelectOption } from "@/components/ui/native-select";
import { expenseTitle, seasonLabel } from "@/features/expenses/components/expense-labels";
import { useExpenses } from "@/features/expenses/queries";
import type { ExpenseListItem, ExpenseQuery } from "@/features/expenses/types";
import { useSeasonOptions } from "@/features/seasons/queries";
import { expenseCategoryLabels } from "@/lib/enum-labels";
import { expenseCategories } from "@/lib/enums";

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;
// Giá trị của ô lọc mùa vụ khi chọn "Chi phí chung của hộ" (`?general=true`).
const GENERAL = "general";

/**
 * `/expenses`: danh sách chi phí ghi tay (Chủ hộ, TV+TC), mới nhất trước, gồm cả khoản chi đã hủy để thấy nhãn "Đã hủy".
 * API không tìm theo chữ nên các ô lọc đặt thẳng trên trang. `?seasonId=` lọc theo mùa vụ khi mở từ trang mùa vụ.
 */
export function ExpenseListPage() {
  const [searchParams, setSearchParams] = useSearchParams();
  const query = readQuery(searchParams);
  const expenses = useExpenses(query);
  // Ô lọc liệt kê mùa vụ đang canh tác (mùa vụ ghi được chi phí); mùa vụ khác lọc được khi mở từ trang mùa vụ.
  const seasonOptions = useSeasonOptions({ operation: "ManageExpenses" });

  // Đổi bộ lọc thì về trang 1; chỉ giữ tham số có giá trị để đường dẫn gọn.
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

  function selectScope(value: string) {
    updateParams({
      general: value === GENERAL ? "true" : undefined,
      seasonId: value !== GENERAL && value !== "" ? value : undefined,
    });
  }

  const scopeValue = query.general ? GENERAL : (query.seasonId ?? "");
  const options = seasonOptions.data ?? [];
  // Mùa vụ đang lọc không nằm trong danh sách (đã chốt, đã hủy): lấy nhãn từ dòng kết quả nếu có.
  const filteredSeasonMissing = query.seasonId !== undefined && !options.some((option) => option.id === query.seasonId);
  const filteredSeasonItem = expenses.data?.items.find((item) => item.seasonId === query.seasonId);

  const isFiltered = Boolean(query.category || query.seasonId || query.general || query.from || query.to);
  const createLink = (
    <Link to="/expenses/new" className={buttonVariants({ className: "min-h-11" })}>
      <PlusIcon data-icon="inline-start" />
      Ghi chi phí
    </Link>
  );

  return (
    <div className="flex flex-col gap-4">
      <PageHeader title="Chi phí" action={createLink} />

      <div className="grid gap-3 md:grid-cols-2 lg:grid-cols-4">
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-category" className="text-sm font-medium">
            Nhóm chi phí
          </label>
          <NativeSelect
            id="filter-category"
            className="w-full"
            value={query.category ?? ""}
            onChange={(event) => updateParams({ category: event.target.value || undefined })}
          >
            <NativeSelectOption value="">Tất cả</NativeSelectOption>
            {expenseCategories.map((category) => (
              <NativeSelectOption key={category} value={category}>
                {expenseCategoryLabels[category]}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-season" className="text-sm font-medium">
            Mùa vụ
          </label>
          <NativeSelect
            id="filter-season"
            className="w-full"
            value={scopeValue}
            onChange={(event) => selectScope(event.target.value)}
          >
            <NativeSelectOption value="">Tất cả</NativeSelectOption>
            <NativeSelectOption value={GENERAL}>Chi phí chung của hộ</NativeSelectOption>
            {filteredSeasonMissing && (
              <NativeSelectOption value={query.seasonId ?? ""}>
                {filteredSeasonItem ? seasonLabel(filteredSeasonItem) : "Mùa vụ đang lọc"}
              </NativeSelectOption>
            )}
            {options.map((option) => (
              <NativeSelectOption key={option.id} value={option.id}>
                {option.label}
              </NativeSelectOption>
            ))}
          </NativeSelect>
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-from" className="text-sm font-medium">
            Từ ngày
          </label>
          <Input
            id="filter-from"
            type="date"
            value={query.from ?? ""}
            max={query.to}
            onChange={(event) => updateParams({ from: event.target.value || undefined })}
          />
        </div>
        <div className="flex flex-col gap-1.5">
          <label htmlFor="filter-to" className="text-sm font-medium">
            Đến ngày
          </label>
          <Input
            id="filter-to"
            type="date"
            value={query.to ?? ""}
            min={query.from}
            onChange={(event) => updateParams({ to: event.target.value || undefined })}
          />
        </div>
      </div>

      {isFiltered && (
        <div>
          <Button type="button" variant="outline" onClick={() => setSearchParams(new URLSearchParams())}>
            Bỏ lọc
          </Button>
        </div>
      )}

      {expenses.isPending ? (
        <LoadingState variant="list" />
      ) : expenses.isError ? (
        <ErrorAlert error={expenses.error} onRetry={() => void expenses.refetch()} />
      ) : expenses.data.items.length === 0 ? (
        isFiltered ? (
          <EmptyState onClearFilters={() => setSearchParams(new URLSearchParams())} />
        ) : (
          <EmptyState message="Chưa có khoản chi nào." action={createLink} />
        )
      ) : (
        <>
          <DataList
            items={expenses.data.items}
            getKey={(item) => item.id}
            getHref={(item) => `/expenses/${item.id}`}
            title={(item) => expenseTitle(item)}
            subtitle={(item) => <Summary item={item} />}
            badge={(item) => (item.voidedAt ? <StatusBadge kind="Voided" /> : <MoneyText amount={item.amount} />)}
            columns={[
              { header: "Ngày", cell: (item) => <DateText value={item.expenseDate} /> },
              { header: "Nhóm", cell: (item) => expenseTitle(item), className: "whitespace-normal" },
              { header: "Mùa vụ", cell: (item) => seasonLabel(item), className: "whitespace-normal" },
              { header: "Số tiền", cell: (item) => <MoneyText amount={item.amount} />, className: "text-right" },
              { header: "", cell: (item) => item.voidedAt && <StatusBadge kind="Voided" /> },
            ]}
          />
          <Pagination meta={expenses.data.meta} onPageChange={(page) => updateParams({ page: String(page) })} />
        </>
      )}
    </div>
  );
}

// Dòng phụ của thẻ trên điện thoại: ngày · mùa vụ; khoản đã hủy thì số tiền xuống dòng này.
function Summary({ item }: { item: ExpenseListItem }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span>
        <DateText value={item.expenseDate} /> · {seasonLabel(item)}
      </span>
      {item.voidedAt && (
        <span className="line-through">
          <MoneyText amount={item.amount} />
        </span>
      )}
    </div>
  );
}

// Đọc tham số trên đường dẫn; giá trị lạ (người dùng tự sửa đường dẫn) thì bỏ qua thay vì gửi lên API.
function readQuery(params: URLSearchParams): ExpenseQuery {
  const page = Number(params.get("page"));
  const from = params.get("from");
  const to = params.get("to");
  const general = params.get("general") === "true";
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    category: expenseCategories.find((category) => category === params.get("category")),
    // API từ chối lọc cùng lúc chi phí chung và một mùa vụ; chi phí chung được ưu tiên.
    general: general || undefined,
    seasonId: general ? undefined : (params.get("seasonId") ?? undefined),
    from: from && DATE_ONLY.test(from) ? from : undefined,
    to: to && DATE_ONLY.test(to) ? to : undefined,
    // Luôn gồm khoản chi đã hủy để hiện nhãn "Đã hủy" thay vì để dòng biến mất.
    includeVoided: true,
  };
}
