import type { FinanceReportResponse } from "@/features/reports/finance/types";
import { expenseCategoryLabels, supplyGroupLabels } from "@/lib/enum-labels";

export interface CostRow {
  key: string;
  label: string;
  amount: number;
}

/**
 * Chi phí vật tư theo nhóm vật tư, chi phí ghi tay theo nhóm và chi phí chung của hộ, gộp một danh sách sắp giảm dần
 * (FR-RPT-FIN-01, FR-RPT-FIN-05).
 */
export function costRows(report: Pick<FinanceReportResponse, "supplyCosts" | "expenseCosts" | "generalExpenseAmount">): CostRow[] {
  const rows: CostRow[] = [
    ...report.supplyCosts.map((item) => ({
      key: `supply-${item.group}`,
      label: `Vật tư: ${supplyGroupLabels[item.group]}`,
      amount: item.amount,
    })),
    ...report.expenseCosts.map((item) => ({
      key: `expense-${item.category}`,
      label: `Chi phí ghi tay: ${expenseCategoryLabels[item.category]}`,
      amount: item.amount,
    })),
  ];
  if (report.generalExpenseAmount !== null) {
    rows.push({ key: "general", label: "Chi phí chung của hộ", amount: report.generalExpenseAmount });
  }
  return rows.sort((a, b) => b.amount - a.amount);
}
