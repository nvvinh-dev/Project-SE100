using System.Net;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Hàm kiểm tra tồn dùng chung cho mọi thao tác làm giảm tồn (FR-SUP-07).</summary>
public class StockLedgerTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "BR-SUP-07: giảm quá tồn khi hộ không bật âm kho trả INSUFFICIENT_STOCK kèm supplyId, available, requested")]
    public async Task CheckDecrease_BelowZero_NotAllowed_Throws()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, name: "Phân NPK");

        var (ledger, warnings) = LedgerFor(household);
        var error = await Assert.ThrowsAsync<AppException>(() => ledger.CheckDecreaseAsync(supply, 5m, 8.5m, "xuất", ct));

        Assert.Equal(ErrorCodes.InsufficientStock, error.Code);
        Assert.Equal("Phân NPK chỉ còn 5 kg, không đủ để xuất 8,5 kg.", error.Message);
        var details = JsonSerializer.SerializeToElement(error.Details);
        Assert.Equal(supply.Id, details.GetProperty("supplyId").GetGuid());
        Assert.Equal(5m, details.GetProperty("available").GetDecimal());
        Assert.Equal(8.5m, details.GetProperty("requested").GetDecimal());
        Assert.Empty(warnings.Warnings);
    }

    [Fact(DisplayName = "BR-SUP-07: giảm quá tồn khi hộ bật âm kho thì cho phép, cảnh báo NEGATIVE_STOCK")]
    public async Task CheckDecrease_BelowZero_Allowed_WarnsNegativeStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await SupplyTestData.SetAllowNegativeStockAsync(factory.Postgres, household, allow: true, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var (ledger, warnings) = LedgerFor(household);
        await ledger.CheckDecreaseAsync(supply, 5m, 8m, "xuất", ct);

        Assert.Equal([WarningCodes.NegativeStock], warnings.Warnings.Select(w => w.Code));
    }

    [Fact(DisplayName = "BR-SUP-08: còn dưới ngưỡng thì cảnh báo LOW_STOCK; bằng ngưỡng không cảnh báo")]
    public async Task CheckDecrease_UnderMinimum_WarnsLowStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, name: "Phân NPK", minStockLevel: 30m);

        var (below, belowWarnings) = LedgerFor(household);
        await below.CheckDecreaseAsync(supply, 100m, 80m, "xuất", ct);
        var (equal, equalWarnings) = LedgerFor(household);
        await equal.CheckDecreaseAsync(supply, 100m, 70m, "xuất", ct);

        var warning = Assert.Single(belowWarnings.Warnings);
        Assert.Equal(WarningCodes.LowStock, warning.Code);
        Assert.Equal("Tồn Phân NPK sau thao tác còn 20 kg, thấp hơn ngưỡng 30 kg.", warning.Message);
        Assert.Empty(equalWarnings.Warnings);
    }

    [Fact(DisplayName = "BR-GEN-12: khóa vật tư không có hoặc của hộ khác trả NOT_FOUND")]
    public async Task LockSupply_OtherHousehold_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var other = await factory.CreateHouseholdAsync(ct);
        var otherSupply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, other, "VT001", ct);

        var (ledger, _) = LedgerFor(household);
        var error = await Assert.ThrowsAsync<AppException>(() => ledger.LockSupplyAsync(otherSupply.Id, ct));

        Assert.Equal(ErrorCodes.NotFound, error.Code);
        Assert.Equal((int)HttpStatusCode.NotFound, ErrorCodes.StatusOf(error.Code));
    }

    [Fact(DisplayName = "BR-GEN-12: vật tư đã nạp từ trước được đọc lại sau khi khóa, không tính trên giá bình quân cũ")]
    public async Task LockSupply_AlreadyTracked_ReloadsLatestValues()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, averageUnitCost: 100m);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var tracked = await db.Supplies.FindAsync([supply.Id], ct);
        await using (var other = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            (await other.Supplies.FindAsync([supply.Id], ct))!.AverageUnitCost = 250m;
            await other.SaveChangesAsync(ct);
        }

        var locked = await new StockLedger(db, new WarningCollector()).LockSupplyAsync(supply.Id, ct);

        Assert.Same(tracked, locked);
        Assert.Equal(250m, locked.AverageUnitCost);
    }

    private (StockLedger Ledger, WarningCollector Warnings) LedgerFor(TestHousehold household)
    {
        var warnings = new WarningCollector();
        var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        return (new StockLedger(db, warnings), warnings);
    }
}
