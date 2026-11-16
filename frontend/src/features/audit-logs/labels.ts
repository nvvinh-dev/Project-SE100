import { userRoleLabels } from "@/lib/enum-labels";
import type { UserRole } from "@/lib/enums";

// Nhãn tiếng Việt của nhật ký, chữ lấy theo tên thao tác nghiệp vụ của glossary mục 11.
// Giá trị action là dữ liệu của nhật ký, không phải enum dùng chung, nên nhãn đặt trong module này.

/** Mọi giá trị `action` của nhật ký, đúng bảng hành động nhật ký của glossary mục 9. */
export const auditActionLabels = {
  "StockTransaction.StockIn": "Nhập kho",
  "StockTransaction.StockOut": "Xuất kho",
  "StockTransaction.Adjust": "Điều chỉnh tồn vật tư",
  "StockTransaction.Void": "Hủy giao dịch kho",
  "Expense.Create": "Ghi chi phí",
  "Expense.Correct": "Điều chỉnh chi phí",
  "Expense.Void": "Hủy khoản chi",
  "HarvestRecord.Create": "Ghi phiếu thu hoạch",
  "HarvestRecord.Correct": "Điều chỉnh phiếu thu hoạch",
  "HarvestRecord.Void": "Hủy phiếu thu hoạch",
  "ProduceStockAdjustment.Adjust": "Điều chỉnh tồn nông sản",
  "ProduceStockAdjustment.Void": "Hủy điều chỉnh tồn nông sản",
  "Sale.Create": "Ghi giao dịch bán",
  "Sale.Correct": "Điều chỉnh giao dịch bán",
  "Sale.Void": "Hủy giao dịch bán",
  "Payment.Create": "Ghi thanh toán",
  "Payment.Void": "Hủy thanh toán",
  "Season.Start": "Bắt đầu mùa vụ",
  "Season.Close": "Chốt mùa vụ",
  "Season.Cancel": "Hủy mùa vụ",
  "Season.Reopen": "Mở lại mùa vụ",
  "SeasonAssignment.Assign": "Phân công",
  "SeasonAssignment.Unassign": "Thu hồi phân công",
  "ApplicationUser.ChangeRole": "Đổi vai trò",
  "ApplicationUser.ChangePermissions": "Đổi quyền bổ sung",
  "ApplicationUser.Lock": "Khóa tài khoản",
  "ApplicationUser.Unlock": "Mở khóa tài khoản",
  "Household.ChangeSettings": "Đổi cài đặt hộ",
  "Household.SupportAccess": "Truy cập hỗ trợ",
  "Report.Export": "Xuất báo cáo",
} as const satisfies Record<string, string>;

export type AuditAction = keyof typeof auditActionLabels;
export const auditActions = Object.keys(auditActionLabels) as AuditAction[];

export function isAuditAction(value: string): value is AuditAction {
  return value in auditActionLabels;
}

/** Nhãn của một `action`; giá trị lạ (dữ liệu cũ) thì hiện nguyên văn. */
export function auditActionLabel(action: string): string {
  return isAuditAction(action) ? auditActionLabels[action] : action;
}

const entityTypeLabels: Record<string, string> = {
  StockTransaction: "Giao dịch kho",
  Expense: "Chi phí",
  HarvestRecord: "Phiếu thu hoạch",
  ProduceStockAdjustment: "Điều chỉnh tồn nông sản",
  Sale: "Giao dịch bán",
  Payment: "Thanh toán",
  Season: "Mùa vụ",
  SeasonAssignment: "Phân công",
  ApplicationUser: "Tài khoản",
  Household: "Hộ",
  Report: "Báo cáo",
};

export function entityTypeLabel(entityType: string): string {
  return entityTypeLabels[entityType] ?? entityType;
}

// Tên trường hay gặp trong giá trị cũ, mới; trường khác hiện nguyên tên như API.
const fieldLabels: Record<string, string> = {
  role: "Vai trò",
  isLocked: "Đã khóa",
  canManageStock: "Quản lý kho",
  canManageFinance: "Quản lý tài chính",
  canCloseSeason: "Chốt mùa vụ",
  allowNegativeStock: "Cho phép âm kho",
  allowOverlappingSeasons: "Cho phép mùa vụ chồng lấn",
  status: "Trạng thái",
  amount: "Số tiền",
  quantity: "Số lượng",
  unitPrice: "Đơn giá",
  note: "Ghi chú",
};

export function fieldLabel(field: string): string {
  return fieldLabels[field] ?? field;
}

/** Một giá trị trong nhật ký thành chữ: đúng/sai thành Có/Không, vai trò thành nhãn tiếng Việt, trống thành "—". */
export function formatAuditValue(field: string, value: unknown): string {
  if (value === null || value === undefined || value === "") {
    return "—";
  }
  if (typeof value === "boolean") {
    return value ? "Có" : "Không";
  }
  if (field === "role" && typeof value === "string" && value in userRoleLabels) {
    return userRoleLabels[value as UserRole];
  }
  if (typeof value === "object") {
    return JSON.stringify(value);
  }
  return String(value);
}
