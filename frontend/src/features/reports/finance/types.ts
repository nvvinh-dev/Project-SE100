import type { ReportFilter, ReportSeasonSummary } from "@/features/reports/common/types";
import type { ExpenseCategory, SalesChannel, SupplyGroup } from "@/lib/enums";

/** Bộ lọc của báo cáo chi phí - doanh thu (FR-RPT-01). */
export type FinanceReportQuery = ReportFilter;

export interface FinanceSupplyCostItem {
  group: SupplyGroup;
  amount: number;
}

export interface FinanceExpenseCostItem {
  category: ExpenseCategory;
  amount: number;
}

/** `channel = null`: giao dịch bán chỉ ghi khách hàng, không ghi kênh. */
export interface FinanceRevenueChannelItem {
  channel: SalesChannel | null;
  amount: number;
}

export interface FinanceSeasonItem {
  season: ReportSeasonSummary;
  supplyCost: number;
  expenseAmount: number;
  revenue: number;
  paidAmount: number;
  outstandingAmount: number;
  profit: number;
}

export interface FinanceCashFlow {
  stockInAmount: number;
  expenseAmount: number;
  cashOut: number;
  cashIn: number;
  unpaidRevenue: number;
}

/** `month` dạng `yyyy-MM`. */
export interface FinanceMonthItem {
  month: string;
  revenue: number;
  cost: number;
}

export interface FinanceReportResponse {
  supplyCosts: FinanceSupplyCostItem[];
  expenseCosts: FinanceExpenseCostItem[];
  /** `null` khi đang lọc theo mùa vụ, thửa hoặc cây trồng (BR-RPT-13). */
  generalExpenseAmount: number | null;
  totalCost: number;
  revenueByChannel: FinanceRevenueChannelItem[];
  totalRevenue: number;
  seasons: FinanceSeasonItem[];
  cancelledSeasons: FinanceSeasonItem[];
  closedProfit: number;
  provisionalProfit: number;
  cashFlow: FinanceCashFlow;
  monthly: FinanceMonthItem[];
}
