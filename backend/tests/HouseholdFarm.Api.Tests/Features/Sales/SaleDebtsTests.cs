using System.Net;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Công nợ bán hàng chưa thu (FR-SAL-07).</summary>
public class SaleDebtsTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string DebtsPath = "/api/sales/debts";

    [Fact(DisplayName = "FR-SAL-07: chỉ có giao dịch còn nợ, tổng đúng; đã thanh toán đủ và đã hủy không xuất hiện")]
    public async Task GetDebts_ReturnsOnlyOutstandingSalesWithTotals()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        // Doanh thu mỗi giao dịch 1.000.000.
        var unpaid = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, saleDate: new DateOnly(2026, 10, 10));
        var partial = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, saleDate: new DateOnly(2026, 10, 12));
        var paid = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        await CreateSaleAsync(factory.Postgres, household, season.Id, ct, voided: true);
        await CreatePaymentAsync(factory.Postgres, household, partial.Id, 400_000m, ct);
        await CreatePaymentAsync(factory.Postgres, household, partial.Id, 500_000m, ct, voided: true);
        await CreatePaymentAsync(factory.Postgres, household, paid.Id, 1_000_000m, ct);

        var response = await factory.CreateClientFor(household.MemberWithAllPermissions).GetAsync(DebtsPath, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(2, data.GetProperty("totalCount").GetInt32());
        Assert.Equal(1_600_000m, data.GetProperty("totalOutstanding").GetDecimal());
        Assert.Equal(
            [unpaid.Id, partial.Id],
            data.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()));
    }

    [Fact(DisplayName = "FR-SAL-07: Thành viên không có TC xem công nợ bị FORBIDDEN")]
    public async Task GetDebts_WithoutFinance_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Member).GetAsync(DebtsPath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }
}
