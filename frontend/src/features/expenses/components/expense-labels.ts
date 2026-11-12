import { expenseCategoryLabels } from "@/lib/enum-labels";
import type { ExpenseCategory } from "@/lib/enums";
import { formatDate } from "@/lib/format";

interface ExpenseCategoryInfo {
  category: ExpenseCategory;
  categoryOther: string | null;
}

interface ExpenseSeasonInfo {
  seasonId: string | null;
  cropName: string | null;
  plotCode: string | null;
  seasonStartDate: string | null;
}

/** Tên khoản chi theo nhóm; nhóm "Khác" có ghi chú thì kèm ghi chú: "Khác · Phí thủy lợi". */
export function expenseTitle(expense: ExpenseCategoryInfo): string {
  const label = expenseCategoryLabels[expense.category];
  return expense.category === "Other" && expense.categoryOther ? `${label} · ${expense.categoryOther}` : label;
}

/** Mùa vụ rút gọn "Cây · Thửa · ngày bắt đầu", hoặc "Chi phí chung" khi không gắn mùa vụ. */
export function seasonLabel(expense: ExpenseSeasonInfo): string {
  if (!expense.seasonId) {
    return "Chi phí chung";
  }
  return [expense.cropName, expense.plotCode, expense.seasonStartDate && formatDate(expense.seasonStartDate)]
    .filter(Boolean)
    .join(" · ");
}
