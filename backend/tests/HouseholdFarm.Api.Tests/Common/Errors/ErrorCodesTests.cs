using System.Reflection;
using HouseholdFarm.Api.Common.Errors;

namespace HouseholdFarm.Api.Tests.Common.Errors;

public class ErrorCodesTests
{
    [Theory(DisplayName = "Mỗi mã lỗi trả đúng mã HTTP theo bảng mã HTTP của quy ước API")]
    [InlineData("VALIDATION_ERROR", 400)]
    [InlineData("UNAUTHENTICATED", 401)]
    [InlineData("INVALID_CREDENTIALS", 401)]
    [InlineData("INVALID_REFRESH_TOKEN", 401)]
    [InlineData("FORBIDDEN", 403)]
    [InlineData("ACCOUNT_LOCKED", 403)]
    [InlineData("NOT_FOUND", 404)]
    [InlineData("CONCURRENCY_CONFLICT", 409)]
    [InlineData("PHONE_ALREADY_USED", 409)]
    [InlineData("CODE_ALREADY_USED", 409)]
    [InlineData("NAME_ALREADY_USED", 409)]
    [InlineData("CONFIRMATION_REQUIRED", 422)]
    [InlineData("ALREADY_VOIDED", 422)]
    [InlineData("CATALOG_INACTIVE", 422)]
    [InlineData("CATALOG_IN_USE", 422)]
    [InlineData("UNIT_LOCKED", 422)]
    [InlineData("SEASON_INVALID_STATE", 422)]
    [InlineData("SEASON_OVERLAP", 422)]
    [InlineData("SEASON_PLOT_BUSY", 422)]
    [InlineData("SEASON_PRODUCE_REMAINING", 422)]
    [InlineData("SEASON_HAS_HARVEST", 422)]
    [InlineData("TASK_INVALID_STATE", 422)]
    [InlineData("ASSIGNEE_NOT_IN_SEASON", 422)]
    [InlineData("INSUFFICIENT_STOCK", 422)]
    [InlineData("STOCK_TX_NOT_LAST", 422)]
    [InlineData("STOCK_TX_MANAGED_BY_TASK", 422)]
    [InlineData("INSUFFICIENT_PRODUCE_STOCK", 422)]
    [InlineData("PRODUCE_STOCK_NEGATIVE", 422)]
    [InlineData("SALE_HAS_PAYMENTS", 422)]
    [InlineData("REVENUE_BELOW_PAID", 422)]
    [InlineData("PAYMENT_EXCEEDS_OUTSTANDING", 422)]
    [InlineData("TOO_MANY_ATTEMPTS", 429)]
    [InlineData("AI_QUOTA_EXCEEDED", 429)]
    [InlineData("INTERNAL_ERROR", 500)]
    [InlineData("AI_UNAVAILABLE", 503)]
    public void StatusOf_MatchesConvention(string code, int status)
    {
        Assert.Equal(status, ErrorCodes.StatusOf(code));
    }

    [Fact(DisplayName = "ErrorCodes có đủ 35 mã của danh mục, mỗi hằng số đều có mã HTTP và tiêu đề tiếng Việt")]
    public void Constants_AllHaveStatusAndTitle()
    {
        var constants = typeof(ErrorCodes)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.IsLiteral)
            .Select(f => (string)f.GetRawConstantValue()!)
            .ToList();

        Assert.Equal(35, constants.Count);
        Assert.Equal(constants.Order(), ErrorCodes.All.Order());
        Assert.All(constants, code => Assert.False(string.IsNullOrWhiteSpace(ErrorCodes.TitleOf(code))));
    }

    [Fact(DisplayName = "AppException với mã không có trong ErrorCodes bị từ chối ngay khi tạo")]
    public void AppException_UnknownCode_Throws()
    {
        Assert.Throws<ArgumentException>(() => new AppException("OUT_OF_STOCK", "Hết hàng."));
    }
}
