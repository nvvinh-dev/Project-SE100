using System.Reflection;
using HouseholdFarm.Api.Common.Auditing;

namespace HouseholdFarm.Api.Tests.Common.Auditing;

public class AuditActionsTests
{
    // Bảng "Hành động nhật ký" của glossary, chép nguyên để phát hiện hằng số thiếu hoặc gõ sai.
    private static readonly string[] _glossaryActions =
    [
        "StockTransaction.StockIn", "StockTransaction.StockOut", "StockTransaction.Adjust", "StockTransaction.Void",
        "Expense.Create", "Expense.Correct", "Expense.Void",
        "HarvestRecord.Create", "HarvestRecord.Correct", "HarvestRecord.Void",
        "ProduceStockAdjustment.Adjust", "ProduceStockAdjustment.Void",
        "Sale.Create", "Sale.Correct", "Sale.Void", "Payment.Create", "Payment.Void",
        "Season.Start", "Season.Close", "Season.Cancel", "Season.Reopen",
        "SeasonAssignment.Assign", "SeasonAssignment.Unassign",
        "ApplicationUser.ChangeRole", "ApplicationUser.ChangePermissions", "ApplicationUser.Lock", "ApplicationUser.Unlock",
        "Household.ChangeSettings", "Household.SupportAccess",
        "Report.Export",
    ];

    [Fact(DisplayName = "AuditActions có đúng 30 hành động của glossary, không thừa không thiếu")]
    public void All_MatchesGlossary()
    {
        Assert.Equal(_glossaryActions.Order(), AuditActions.All.Order());
    }

    [Fact(DisplayName = "Tên hằng số là tên hành động bỏ dấu chấm, ví dụ SeasonClose = Season.Close")]
    public void ConstantNames_AreActionWithoutDot()
    {
        var constants = typeof(AuditActions)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral);

        Assert.All(constants, f => Assert.Equal(((string)f.GetRawConstantValue()!).Replace(".", string.Empty), f.Name));
    }

    [Fact(DisplayName = "IsDefined chỉ nhận hành động đã khai báo")]
    public void IsDefined_RejectsUnknownAction()
    {
        Assert.True(AuditActions.IsDefined(AuditActions.SeasonClose));
        Assert.False(AuditActions.IsDefined("Season.Delete"));
        Assert.False(AuditActions.IsDefined("season.close"));
    }
}
