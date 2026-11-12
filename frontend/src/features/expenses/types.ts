import type { PageQuery } from "@/lib/api-client";
import type { ExpenseCategory, SeasonStatus } from "@/lib/enums";

// Trùng tên DTO backend (Features/Expenses/ExpenseDtos.cs).

/** Chi tiết một khoản chi. `seasonId` trống nghĩa là chi phí chung của hộ, khi đó các trường mùa vụ cũng trống. */
export interface ExpenseResponse {
  id: string;
  seasonId: string | null;
  cropName: string | null;
  plotCode: string | null;
  seasonStartDate: string | null;
  /** Mùa vụ không đang canh tác thì không điều chỉnh, hủy được khoản chi (BR-EXP-02). */
  seasonStatus: SeasonStatus | null;
  category: ExpenseCategory;
  categoryOther: string | null;
  amount: number;
  expenseDate: string;
  note: string | null;
  voidedAt: string | null;
  voidReason: string | null;
  createdAt: string;
  /** Gửi kèm khi điều chỉnh để không ghi đè thay đổi của người khác (BR-GEN-11). */
  version: number;
}

export interface ExpenseListItem {
  id: string;
  category: ExpenseCategory;
  categoryOther: string | null;
  amount: number;
  expenseDate: string;
  seasonId: string | null;
  cropName: string | null;
  plotCode: string | null;
  seasonStartDate: string | null;
  note: string | null;
  voidedAt: string | null;
}

/** `from`, `to` lọc theo ngày phát sinh; `general=true` chỉ chi phí chung của hộ. */
export interface ExpenseQuery extends PageQuery {
  category?: ExpenseCategory;
  seasonId?: string;
  general?: boolean;
  from?: string;
  to?: string;
  includeVoided?: boolean;
}

export interface CreateExpenseRequest {
  requestId: string;
  seasonId: string | null;
  category: ExpenseCategory;
  categoryOther: string | null;
  amount: number;
  expenseDate: string;
  note: string | null;
}

export interface CorrectExpenseRequest {
  seasonId: string | null;
  category: ExpenseCategory;
  categoryOther: string | null;
  amount: number;
  expenseDate: string;
  note: string | null;
  reason: string;
  version: number;
}

export interface VoidExpenseRequest {
  reason: string;
}
