using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Tạo khách hàng, phiếu thu hoạch, giao dịch bán, thanh toán thẳng trong database và dựng body yêu cầu.</summary>
public static class SaleTestData
{
    public const string SalesPath = "/api/sales";

    /// <summary>Hộ mới kèm thửa TD001 và một mùa vụ ở trạng thái <paramref name="status"/> (bắt đầu 2026-10-01).</summary>
    public static async Task<(TestHousehold Household, Season Season)> ArrangeSeasonAsync(
        ApiFactory factory,
        SeasonStatus status,
        CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, status, ct);
        return (household, season);
    }

    public static async Task<Customer> CreateCustomerAsync(
        PostgresFixture postgres,
        TestHousehold household,
        CancellationToken ct,
        string name = "Chị Lan",
        RecordStatus status = RecordStatus.Active)
    {
        var customer = new Customer { Name = name, Status = status };
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Customers.Add(customer);
        await db.SaveChangesAsync(ct);
        return customer;
    }

    public static async Task<HarvestRecord> CreateHarvestAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Guid seasonId,
        decimal quantity,
        CancellationToken ct,
        QualityGrade grade = QualityGrade.Grade1,
        bool voided = false)
    {
        var harvest = new HarvestRecord
        {
            SeasonId = seasonId,
            HarvestDate = new DateOnly(2026, 10, 20),
            QualityGrade = grade,
            Quantity = quantity,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.HarvestRecords.Add(harvest);
        await db.SaveChangesAsync(ct);
        return harvest;
    }

    public static async Task<Sale> CreateSaleAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Guid seasonId,
        CancellationToken ct,
        decimal quantity = 100m,
        decimal unitPrice = 10_000m,
        QualityGrade grade = QualityGrade.Grade1,
        Guid? customerId = null,
        SalesChannel? channel = SalesChannel.Trader,
        DateOnly? saleDate = null,
        bool voided = false)
    {
        var sale = new Sale
        {
            SeasonId = seasonId,
            CustomerId = customerId,
            Channel = channel,
            QualityGrade = grade,
            Quantity = quantity,
            UnitPrice = unitPrice,
            SaleDate = saleDate ?? new DateOnly(2026, 10, 25),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Sales.Add(sale);
        await db.SaveChangesAsync(ct);
        return sale;
    }

    public static async Task<Payment> CreatePaymentAsync(
        PostgresFixture postgres,
        TestHousehold household,
        Guid saleId,
        decimal amount,
        CancellationToken ct,
        bool voided = false)
    {
        var payment = new Payment
        {
            SaleId = saleId,
            Amount = amount,
            PaymentDate = new DateOnly(2026, 10, 26),
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? TimeProvider.System.GetUtcNow().UtcDateTime : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Payments.Add(payment);
        await db.SaveChangesAsync(ct);
        return payment;
    }

    public static object Create(
        Guid seasonId,
        decimal quantity = 100m,
        decimal unitPrice = 12_000m,
        string? channel = "Trader",
        Guid? customerId = null,
        string grade = "Grade1",
        DateOnly? saleDate = null,
        Guid? requestId = null,
        IReadOnlyList<Guid>? harvestRecordIds = null) => new
    {
        requestId = requestId ?? Guid.CreateVersion7(),
        seasonId,
        customerId,
        channel,
        channelOther = (string?)null,
        qualityGrade = grade,
        quantity,
        unitPrice,
        saleDate = saleDate ?? new DateOnly(2026, 10, 25),
        note = "Bán cho thương lái",
        harvestRecordIds,
    };
}
