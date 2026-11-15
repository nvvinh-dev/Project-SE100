using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Finance;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Reports;

/// <summary>Số liệu tài chính theo mùa vụ dùng chung (NOI-04, BR-RPT-01, BR-RPT-03, BR-RPT-04).</summary>
public class SeasonFinanceServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "BR-RPT-01: đủ 6 số liệu theo công thức, chi phí vật tư 330.000đ theo ví dụ giá vốn, loại bản ghi Void")]
    public async Task Get_ComputesAllFiguresAndExcludesVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var otherSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, otherPlot, SeasonStatus.InProgress, ct);

        // Ví dụ giá vốn: nhập 10 kg giá 20.000đ, 10 kg giá 24.000đ, xuất 15 kg cho mùa lúa giá 22.000đ, nhập 5 kg giá 30.000đ.
        var fertilizer = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, averageUnitCost: 26_000m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, fertilizer, StockTransactionType.In, 10m, ct, unitCost: 20_000m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, fertilizer, StockTransactionType.In, 10m, ct, unitCost: 24_000m);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, fertilizer, StockTransactionType.Out, -15m, ct, seasonId: season.Id, unitCost: 22_000m);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, fertilizer, StockTransactionType.In, 5m, ct, unitCost: 30_000m);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, fertilizer, StockTransactionType.Out, -2m, ct, seasonId: season.Id, unitCost: 26_000m, voided: true);
        await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, household, fertilizer, StockTransactionType.Out, -1m, ct, seasonId: otherSeason.Id, unitCost: 26_000m);

        await CreateExpenseAsync(household, season.Id, 150_000m, ct);
        await CreateExpenseAsync(household, season.Id, 50_000m, ct);
        await CreateExpenseAsync(household, season.Id, 999_000m, ct, voided: true);
        await CreateExpenseAsync(household, seasonId: null, 70_000m, ct);

        // 500 × 8.000 = 4.000.000; 0,333 × 1.000,5 = 333,1665 làm tròn 333,17.
        var sale = await CreateSaleAsync(household, season.Id, 500m, 8_000m, ct);
        await CreateSaleAsync(household, season.Id, 0.333m, 1_000.5m, ct);
        var voidedSale = await CreateSaleAsync(household, season.Id, 100m, 9_000m, ct, voided: true);
        await CreatePaymentAsync(household, sale.Id, 1_500_000m, ct);
        await CreatePaymentAsync(household, sale.Id, 200_000m, ct, voided: true);
        await CreatePaymentAsync(household, voidedSale.Id, 300_000m, ct, voided: true);

        var result = await CreateService(TestCurrentUser.For(household.Owner)).GetAsync([season.Id], ct);

        var finance = Assert.Single(result);
        Assert.Equal(season.Id, finance.SeasonId);
        Assert.Equal(SeasonStatus.InProgress, finance.Status);
        Assert.Equal(330_000m, finance.SupplyCost);
        Assert.Equal(200_000m, finance.ExpenseAmount);
        Assert.Equal(4_000_333.17m, finance.Revenue);
        Assert.Equal(1_500_000m, finance.PaidAmount);
        Assert.Equal(2_500_333.17m, finance.OutstandingAmount);
        Assert.Equal(3_470_333.17m, finance.Profit);
    }

    [Fact(DisplayName = "BR-RPT-03: nhiều mùa vụ trả mỗi mùa một dòng kèm trạng thái, mùa vụ chưa phát sinh trả 0, id trùng chỉ một dòng")]
    public async Task Get_ManySeasons_ReturnsOneRowPerSeasonWithStatus()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var closedPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var closed = await TestData.CreateSeasonAsync(factory.Postgres, household, closedPlot, SeasonStatus.Closed, ct);
        var cancelledPlot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var cancelled = await TestData.CreateSeasonAsync(factory.Postgres, household, cancelledPlot, SeasonStatus.Cancelled, ct);
        await CreateExpenseAsync(household, closed.Id, 120_000m, ct);
        var sale = await CreateSaleAsync(household, closed.Id, 10m, 50_000m, ct);
        await CreatePaymentAsync(household, sale.Id, 500_000m, ct);
        await CreateExpenseAsync(household, cancelled.Id, 30_000m, ct);

        var result = await CreateService(TestCurrentUser.For(household.Owner))
            .GetAsync([closed.Id, cancelled.Id, closed.Id], ct);

        Assert.Equal(2, result.Count);
        var closedFinance = result.Single(r => r.SeasonId == closed.Id);
        Assert.Equal(SeasonStatus.Closed, closedFinance.Status);
        Assert.Equal(0m, closedFinance.SupplyCost);
        Assert.Equal(500_000m, closedFinance.Revenue);
        Assert.Equal(0m, closedFinance.OutstandingAmount);
        Assert.Equal(380_000m, closedFinance.Profit);
        var cancelledFinance = result.Single(r => r.SeasonId == cancelled.Id);
        Assert.Equal(SeasonStatus.Cancelled, cancelledFinance.Status);
        Assert.Equal(0m, cancelledFinance.Revenue);
        Assert.Equal(0m, cancelledFinance.PaidAmount);
        Assert.Equal(-30_000m, cancelledFinance.Profit);
    }

    [Fact(DisplayName = "BR-RPT-04: Thành viên có TC xem được cả mùa vụ ngoài phân công; danh sách rỗng trả rỗng")]
    public async Task Get_MemberWithFinance_SeesUnassignedSeason()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await CreateSaleAsync(household, season.Id, 2m, 25_000m, ct);
        var service = CreateService(TestCurrentUser.For(household.MemberWithAllPermissions));

        var finance = Assert.Single(await service.GetAsync([season.Id], ct));

        Assert.Equal(50_000m, finance.Revenue);
        Assert.Empty(await service.GetAsync([], ct));
    }

    [Fact(DisplayName = "BR-RPT-04: Thành viên không có TC, Nhân công, Quản trị hệ thống bị FORBIDDEN")]
    public async Task Get_WithoutFinancePermission_ThrowsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var admin = await factory.CreateSystemAdminAsync(ct);

        foreach (var user in new[]
                 {
                     TestCurrentUser.For(household.Member, season.Id),
                     TestCurrentUser.For(household.Worker, season.Id),
                     TestCurrentUser.For(admin),
                 })
        {
            var ex = await Assert.ThrowsAsync<AppException>(() => CreateService(user).GetAsync([season.Id], ct));
            Assert.Equal(ErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact(DisplayName = "BR-GEN-02: mùa vụ của hộ khác trả NOT_FOUND")]
    public async Task Get_SeasonOfOtherHousehold_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, otherHousehold, "TD001", ct);
        var otherSeason = await TestData.CreateSeasonAsync(factory.Postgres, otherHousehold, otherPlot, SeasonStatus.InProgress, ct);

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            CreateService(TestCurrentUser.For(household.Owner)).GetAsync([season.Id, otherSeason.Id], ct));

        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    private SeasonFinanceService CreateService(ICurrentUser user) =>
        new(factory.Postgres.CreateDbContext(user), user);

    private async Task CreateExpenseAsync(
        TestHousehold household,
        Guid? seasonId,
        decimal amount,
        CancellationToken ct,
        bool voided = false)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Expenses.Add(new Expense
        {
            SeasonId = seasonId,
            Category = ExpenseCategory.Labor,
            Amount = amount,
            ExpenseDate = new DateOnly(2026, 10, 10),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        });
        await db.SaveChangesAsync(ct);
    }

    private async Task<Sale> CreateSaleAsync(
        TestHousehold household,
        Guid seasonId,
        decimal quantity,
        decimal unitPrice,
        CancellationToken ct,
        bool voided = false)
    {
        var sale = new Sale
        {
            SeasonId = seasonId,
            Channel = SalesChannel.Trader,
            QualityGrade = QualityGrade.Grade1,
            Quantity = quantity,
            UnitPrice = unitPrice,
            SaleDate = new DateOnly(2026, 11, 1),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);
        return sale;
    }

    private async Task CreatePaymentAsync(
        TestHousehold household,
        Guid saleId,
        decimal amount,
        CancellationToken ct,
        bool voided = false)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Payments.Add(new Payment
        {
            SaleId = saleId,
            Amount = amount,
            PaymentDate = new DateOnly(2026, 11, 2),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        });
        await db.SaveChangesAsync(ct);
    }
}
