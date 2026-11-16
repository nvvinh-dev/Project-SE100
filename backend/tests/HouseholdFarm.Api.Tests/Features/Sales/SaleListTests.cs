using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Danh sách, chi tiết giao dịch bán (FR-SAL-09).</summary>
public class SaleListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-SAL-09: lọc theo trạng thái thanh toán; thanh toán đã Void không tính vào đã thu")]
    public async Task GetList_FilterByPaymentStatus_UsesNonVoidedPayments()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        // Doanh thu mỗi giao dịch 1.000.000.
        var unpaid = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var partial = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var paid = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var paidByVoidedOnly = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        await CreatePaymentAsync(factory.Postgres, household, partial.Id, 400_000m, ct);
        await CreatePaymentAsync(factory.Postgres, household, paid.Id, 600_000m, ct);
        await CreatePaymentAsync(factory.Postgres, household, paid.Id, 400_000m, ct);
        await CreatePaymentAsync(factory.Postgres, household, paidByVoidedOnly.Id, 1_000_000m, ct, voided: true);
        var client = factory.CreateClientFor(household.Owner);

        async Task<List<Guid>> IdsAsync(string status) =>
            Ids(await (await client.GetAsync($"{SalesPath}?paymentStatus={status}", ct)).ReadJsonAsync(ct));

        Assert.Equal([paid.Id], await IdsAsync("Paid"));
        Assert.Equal([partial.Id], await IdsAsync("PartiallyPaid"));
        Assert.Equal(new HashSet<Guid> { unpaid.Id, paidByVoidedOnly.Id }, (await IdsAsync("Unpaid")).ToHashSet());

        var partialItem = (await (await client.GetAsync($"{SalesPath}?paymentStatus=PartiallyPaid", ct)).ReadJsonAsync(ct))
            .GetProperty("data")[0];
        Assert.Equal(1_000_000m, partialItem.GetProperty("revenue").GetDecimal());
        Assert.Equal(400_000m, partialItem.GetProperty("paidAmount").GetDecimal());
        Assert.Equal(600_000m, partialItem.GetProperty("outstandingAmount").GetDecimal());
    }

    [Fact(DisplayName = "FR-SAL-09: lọc theo mùa vụ, khách hàng, kênh bán, khoảng ngày bán; mặc định ẩn giao dịch đã hủy")]
    public async Task GetList_FilterByEachParameter_ReturnsMatches()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var otherSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot2, SeasonStatus.InProgress, ct);
        var customer = await CreateCustomerAsync(factory.Postgres, household, ct);
        var trader = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, saleDate: new DateOnly(2026, 10, 10));
        var retailCustomer = await CreateSaleAsync(
            factory.Postgres, household, season.Id, ct, customerId: customer.Id, channel: SalesChannel.Retail,
            saleDate: new DateOnly(2026, 10, 20));
        var otherSeasonSale = await CreateSaleAsync(factory.Postgres, household, otherSeason.Id, ct, saleDate: new DateOnly(2026, 10, 30));
        var voided = await CreateSaleAsync(factory.Postgres, household, season.Id, ct, voided: true);
        var client = factory.CreateClientFor(household.MemberWithAllPermissions);

        async Task<List<Guid>> IdsAsync(string query) =>
            Ids(await (await client.GetAsync($"{SalesPath}?{query}", ct)).ReadJsonAsync(ct));

        Assert.Equal([retailCustomer.Id, trader.Id], await IdsAsync($"seasonId={season.Id}"));
        Assert.Equal([retailCustomer.Id], await IdsAsync($"customerId={customer.Id}"));
        Assert.Equal([otherSeasonSale.Id, trader.Id], await IdsAsync("channel=Trader"));
        Assert.Equal([retailCustomer.Id, trader.Id], await IdsAsync("from=2026-10-10&to=2026-10-20"));
        Assert.Contains(voided.Id, await IdsAsync("includeVoided=true"));
        Assert.DoesNotContain(voided.Id, await IdsAsync(string.Empty));
    }

    [Fact(DisplayName = "FR-SAL-09: Thành viên không có TC, Nhân công xem giao dịch bán bị FORBIDDEN")]
    public async Task GetList_WithoutFinance_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            var response = await factory.CreateClientFor(user).GetAsync(SalesPath, ct);
            await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "BR-GEN-02: giao dịch bán của hộ khác trả NOT_FOUND")]
    public async Task Get_OtherHouseholdSale_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var (otherHousehold, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var sale = await CreateSaleAsync(factory.Postgres, otherHousehold, season.Id, ct);
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).GetAsync($"{SalesPath}/{sale.Id}", ct);

        await response.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    private static List<Guid> Ids(JsonElement body) =>
        body.GetProperty("data").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
}
