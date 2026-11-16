using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Sales;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using static HouseholdFarm.Api.Tests.Features.Sales.SaleTestData;

namespace HouseholdFarm.Api.Tests.Features.Sales;

/// <summary>Điểm nối gỡ liên kết bán - thu hoạch khi phiếu bị Void hoặc đổi loại (BR-HAR-03, BR-HAR-04).</summary>
public class SaleHarvestLinkServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "ISaleHarvestLinkService: xóa đúng các dòng liên kết của phiếu, giữ liên kết của phiếu khác")]
    public async Task RemoveByHarvestRecord_DeletesOnlyThatRecordLinks()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var target = await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var other = await CreateHarvestAsync(factory.Postgres, household, season.Id, 200m, ct);
        var sale1 = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        var sale2 = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        await using (var arrange = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            arrange.SaleHarvestLinks.AddRange(
                new SaleHarvestLink { SaleId = sale1.Id, HarvestRecordId = target.Id },
                new SaleHarvestLink { SaleId = sale2.Id, HarvestRecordId = target.Id },
                new SaleHarvestLink { SaleId = sale2.Id, HarvestRecordId = other.Id });
            await arrange.SaveChangesAsync(ct);
        }

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var removedSaleIds = await new SaleHarvestLinkService(db).RemoveByHarvestRecordAsync(target.Id, ct);

        Assert.Equal(new HashSet<Guid> { sale1.Id, sale2.Id }, removedSaleIds.ToHashSet());
        await using var check = factory.Postgres.CreateDbContext();
        var remaining = await check.SaleHarvestLinks.IgnoreQueryFilters()
            .Where(l => l.Sale.SeasonId == season.Id)
            .Select(l => new { l.SaleId, l.HarvestRecordId })
            .ToListAsync(ct);
        Assert.Equal([new { SaleId = sale2.Id, HarvestRecordId = other.Id }], remaining);
    }

    [Fact(DisplayName = "ISaleHarvestLinkService: tham gia giao dịch của bên gọi, bên gọi rollback thì liên kết còn nguyên")]
    public async Task RemoveByHarvestRecord_InCallerTransaction_RollsBackWithCaller()
    {
        var ct = TestContext.Current.CancellationToken;
        var (household, season) = await ArrangeSeasonAsync(factory, SeasonStatus.InProgress, ct);
        var harvest = await CreateHarvestAsync(factory.Postgres, household, season.Id, 300m, ct);
        var sale = await CreateSaleAsync(factory.Postgres, household, season.Id, ct);
        await using (var arrange = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            arrange.SaleHarvestLinks.Add(new SaleHarvestLink { SaleId = sale.Id, HarvestRecordId = harvest.Id });
            await arrange.SaveChangesAsync(ct);
        }

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await new SaleHarvestLinkService(db).RemoveByHarvestRecordAsync(harvest.Id, ct);
            await tx.RollbackAsync(ct);
        }

        await using var check = factory.Postgres.CreateDbContext();
        Assert.True(await check.SaleHarvestLinks.IgnoreQueryFilters().AnyAsync(l => l.HarvestRecordId == harvest.Id, ct));
    }
}
