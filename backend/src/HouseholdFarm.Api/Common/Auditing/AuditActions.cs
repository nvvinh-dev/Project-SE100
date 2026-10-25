using System.Reflection;

namespace HouseholdFarm.Api.Common.Auditing;

/// <summary>
/// Giá trị <c>action</c> của nhật ký thao tác, dạng <c>&lt;Entity&gt;.&lt;Thao tác&gt;</c>, đúng bảng "Hành động nhật ký" của glossary.
/// Thêm thao tác cần ghi nhật ký thì bổ sung bảng đó trước, rồi thêm hằng số vào đây.
/// </summary>
public static class AuditActions
{
    public const string StockTransactionStockIn = "StockTransaction.StockIn";

    // Cả xuất thủ công lẫn xuất sinh từ công việc.
    public const string StockTransactionStockOut = "StockTransaction.StockOut";
    public const string StockTransactionAdjust = "StockTransaction.Adjust";
    public const string StockTransactionVoid = "StockTransaction.Void";

    public const string ExpenseCreate = "Expense.Create";
    public const string ExpenseCorrect = "Expense.Correct";
    public const string ExpenseVoid = "Expense.Void";

    public const string HarvestRecordCreate = "HarvestRecord.Create";
    public const string HarvestRecordCorrect = "HarvestRecord.Correct";
    public const string HarvestRecordVoid = "HarvestRecord.Void";

    public const string ProduceStockAdjustmentAdjust = "ProduceStockAdjustment.Adjust";
    public const string ProduceStockAdjustmentVoid = "ProduceStockAdjustment.Void";

    public const string SaleCreate = "Sale.Create";
    public const string SaleCorrect = "Sale.Correct";
    public const string SaleVoid = "Sale.Void";
    public const string PaymentCreate = "Payment.Create";
    public const string PaymentVoid = "Payment.Void";

    public const string SeasonStart = "Season.Start";
    public const string SeasonClose = "Season.Close";
    public const string SeasonCancel = "Season.Cancel";
    public const string SeasonReopen = "Season.Reopen";

    public const string SeasonAssignmentAssign = "SeasonAssignment.Assign";
    public const string SeasonAssignmentUnassign = "SeasonAssignment.Unassign";

    public const string ApplicationUserChangeRole = "ApplicationUser.ChangeRole";
    public const string ApplicationUserChangePermissions = "ApplicationUser.ChangePermissions";
    public const string ApplicationUserLock = "ApplicationUser.Lock";
    public const string ApplicationUserUnlock = "ApplicationUser.Unlock";

    public const string HouseholdChangeSettings = "Household.ChangeSettings";

    // household_id là hộ được xem, không phải hộ của người đang đăng nhập (BR-USR-16); IAuditLogger.Log lấy hộ của
    // người đang đăng nhập nên chưa ghi được thao tác này, phần ghi riêng làm cùng FR-ADM-04.
    public const string HouseholdSupportAccess = "Household.SupportAccess";

    public const string ReportExport = "Report.Export";

    private static readonly HashSet<string> _all = typeof(AuditActions)
        .GetFields(BindingFlags.Public | BindingFlags.Static)
        .Where(f => f.IsLiteral)
        .Select(f => (string)f.GetRawConstantValue()!)
        .ToHashSet();

    /// <summary>Tất cả giá trị <c>action</c> đã khai báo.</summary>
    public static IReadOnlyCollection<string> All => _all;

    public static bool IsDefined(string action) => _all.Contains(action);
}
