using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Tạo vật tư, giao dịch kho thẳng trong database, không qua API.</summary>
public static class SupplyTestData
{
    public static async Task<Supply> CreateSupplyAsync(
        PostgresFixture postgres,
        TestHousehold household,
        string code,
        CancellationToken ct,
        string? name = null,
        SupplyGroup group = SupplyGroup.Fertilizer,
        Unit unit = Unit.Kilogram,
        decimal minStockLevel = 0m,
        decimal? referencePrice = null,
        decimal averageUnitCost = 0m,
        RecordStatus status = RecordStatus.Active)
    {
        var supply = new Supply
        {
            Code = code,
            Name = name ?? $"Vật tư {code}",
            Group = group,
            Unit = unit,
            MinStockLevel = minStockLevel,
            ReferencePrice = referencePrice,
            AverageUnitCost = averageUnitCost,
            Status = status,
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Supplies.Add(supply);
        await db.SaveChangesAsync(ct);
        return supply;
    }

    /// <summary>
    /// Tạo một giao dịch kho, <paramref name="quantity"/> có dấu như trong database.
    /// Giao dịch <c>Out</c> cần <paramref name="seasonId"/>.
    /// </summary>
    public static async Task<StockTransaction> CreateTransactionAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Supply supply,
        StockTransactionType type,
        decimal quantity,
        CancellationToken ct,
        Guid? seasonId = null,
        decimal unitCost = 0m,
        bool voided = false)
    {
        var transaction = new StockTransaction
        {
            SupplyId = supply.Id,
            Type = type,
            TransactionDate = new DateOnly(2026, 10, 1),
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = decimal.Round(Math.Abs(quantity) * unitCost, 2, MidpointRounding.AwayFromZero),
            SeasonId = seasonId,
            Reason = type == StockTransactionType.Adjustment ? "Kiểm kê" : null,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.StockTransactions.Add(transaction);
        await db.SaveChangesAsync(ct);
        return transaction;
    }

    /// <summary>Bật hoặc tắt cài đặt cho phép âm kho của hộ (BR-SUP-07).</summary>
    public static async Task SetAllowNegativeStockAsync(
        PostgresFixture postgres,
        TestHousehold household,
        bool allow,
        CancellationToken ct)
    {
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var h = await db.Households.FirstAsync(h => h.Id == household.Household.Id, ct);
        h.Settings.AllowNegativeStock = allow;
        await db.SaveChangesAsync(ct);
    }
}
