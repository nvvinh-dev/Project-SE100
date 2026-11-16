using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Harvests;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Harvests;

/// <summary>Kiểm thử điểm nối tồn nông sản (FR-HAR-05, FR-HAR-08, TC-11, TC-12, TC-15).</summary>
public class ProduceStockServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-HAR-05: GetStockAsync tính đúng tổng thu - bán - điều chỉnh, loại bản ghi Void, và kiểm tra quyền/phạm vi")]
    public async Task GetStock_ComputesCorrectlyAndEnforcesScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.HarvestRecords.AddRange(
                new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade1, Quantity = 1000m, RequestId = Guid.CreateVersion7() },
                new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 3), QualityGrade = QualityGrade.Grade1, Quantity = 200m, RequestId = Guid.CreateVersion7(), VoidedAt = DateTime.UtcNow, VoidReason = "Hủy" },
                new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 4), QualityGrade = QualityGrade.Grade2, Quantity = 300m, RequestId = Guid.CreateVersion7() });
            db.Sales.Add(new Sale
            {
                SeasonId = season.Id,
                Channel = SalesChannel.Trader,
                QualityGrade = QualityGrade.Grade1,
                Quantity = 400m,
                UnitPrice = 8_000m,
                SaleDate = new DateOnly(2026, 10, 5),
                RequestId = Guid.CreateVersion7(),
            });
            db.ProduceStockAdjustments.Add(new ProduceStockAdjustment
            {
                SeasonId = season.Id,
                QualityGrade = QualityGrade.Grade1,
                Quantity = 50m,
                Reason = ProduceAdjustmentReason.Loss,
                RequestId = Guid.CreateVersion7(),
            });
            await db.SaveChangesAsync(ct);
        }

        // Chủ hộ -> tính đủ 3 hạng chất lượng
        await using (var ownerDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var service = new ProduceStockService(ownerDb, TestCurrentUser.For(household.Owner));
            var stocks = await service.GetStockAsync(season.Id, ct);

            Assert.Equal(3, stocks.Count);
            var g1 = stocks.Single(s => s.QualityGrade == QualityGrade.Grade1);
            var g2 = stocks.Single(s => s.QualityGrade == QualityGrade.Grade2);
            var g3 = stocks.Single(s => s.QualityGrade == QualityGrade.Grade3);
            Assert.Equal(1000m, g1.TotalHarvested);
            Assert.Equal(400m, g1.TotalSold);
            Assert.Equal(50m, g1.TotalAdjusted);
            Assert.Equal(550m, g1.Quantity);
            Assert.Equal(300m, g2.Quantity);
            Assert.Equal(0m, g3.Quantity);
        }

        // Thành viên ngoài phân công (không có ManageFinance) -> NOT_FOUND
        await using (var memberDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Member)))
        {
            var service = new ProduceStockService(memberDb, TestCurrentUser.For(household.Member));
            var ex = await Assert.ThrowsAsync<AppException>(() => service.GetStockAsync(season.Id, ct));
            Assert.Equal(ErrorCodes.NotFound, ex.Code);
        }

        // Thành viên có ManageFinance ngoài phân công (BR-USR-13) -> xem được
        await using (var finDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.MemberWithAllPermissions)))
        {
            var service = new ProduceStockService(finDb, TestCurrentUser.For(household.MemberWithAllPermissions));
            var stocks = await service.GetStockAsync(season.Id, ct);
            Assert.Equal(550m, stocks.Single(s => s.QualityGrade == QualityGrade.Grade1).Quantity);
        }

        // Nhân công -> FORBIDDEN
        await using (var workerDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Worker)))
        {
            var service = new ProduceStockService(workerDb, TestCurrentUser.For(household.Worker));
            var ex = await Assert.ThrowsAsync<AppException>(() => service.GetStockAsync(season.Id, ct));
            Assert.Equal(ErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact(DisplayName = "FR-HAR-08, TC-11, TC-12: EnsureSufficientStockForSaleAsync chặn vượt tồn và không bù trừ giữa các loại chất lượng")]
    public async Task EnsureSufficientStockForSale_EnforcesPerGradeAndExcludesCurrentSale()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        Guid existingSaleId;
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.HarvestRecords.AddRange(
                new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade1, Quantity = 500m, RequestId = Guid.CreateVersion7() },
                new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade2, Quantity = 300m, RequestId = Guid.CreateVersion7() });

            var sale = new Sale
            {
                SeasonId = season.Id,
                Channel = SalesChannel.Trader,
                QualityGrade = QualityGrade.Grade1,
                Quantity = 350m,
                UnitPrice = 9_000m,
                SaleDate = new DateOnly(2026, 10, 4),
                RequestId = Guid.CreateVersion7(),
            };
            db.Sales.Add(sale);
            await db.SaveChangesAsync(ct);
            existingSaleId = sale.Id;
        }

        await using var verifyDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var service = new ProduceStockService(verifyDb, TestCurrentUser.For(household.Owner));

        // Bán thêm 200 Grade1 trong khi chỉ còn 150 (dù Grade2 còn 300) -> INSUFFICIENT_PRODUCE_STOCK
        var ex = await Assert.ThrowsAsync<AppException>(
            () => service.EnsureSufficientStockForSaleAsync(season.Id, QualityGrade.Grade1, 200m, currentSaleId: null, ct));
        Assert.Equal(ErrorCodes.InsufficientProduceStock, ex.Code);

        // Đính chính giao dịch bán cũ từ 350 -> 450 (<= 500) -> hợp lệ vì hoàn lại currentSaleId
        await service.EnsureSufficientStockForSaleAsync(season.Id, QualityGrade.Grade1, 450m, existingSaleId, ct);

        // Đính chính giao dịch bán cũ vượt tổng 500 -> INSUFFICIENT_PRODUCE_STOCK
        var overEx = await Assert.ThrowsAsync<AppException>(
            () => service.EnsureSufficientStockForSaleAsync(season.Id, QualityGrade.Grade1, 550m, existingSaleId, ct));
        Assert.Equal(ErrorCodes.InsufficientProduceStock, overEx.Code);
    }

    [Fact(DisplayName = "FR-HAR-08 & BR-SAL-05: ValidateHarvestLinksForSaleAsync chấp nhận phiếu hợp lệ và báo VALIDATION_ERROR với phiếu không hợp lệ")]
    public async Task ValidateHarvestLinksForSale_ChecksSeasonGradeAndVoided()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, otherHousehold, "TD999", ct);

        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var season2 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot2, SeasonStatus.InProgress, ct);
        var otherSeason = await TestData.CreateSeasonAsync(factory.Postgres, otherHousehold, otherPlot, SeasonStatus.InProgress, ct);

        Guid validId, wrongGradeId, wrongSeasonId, voidedId, otherHouseId;
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var valid = new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade1, Quantity = 100m, RequestId = Guid.CreateVersion7() };
            var wrongGrade = new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade2, Quantity = 100m, RequestId = Guid.CreateVersion7() };
            var wrongSeason = new HarvestRecord { SeasonId = season2.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade1, Quantity = 100m, RequestId = Guid.CreateVersion7() };
            var voided = new HarvestRecord { SeasonId = season.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade1, Quantity = 100m, RequestId = Guid.CreateVersion7(), VoidedAt = DateTime.UtcNow, VoidReason = "Hủy" };
            db.HarvestRecords.AddRange(valid, wrongGrade, wrongSeason, voided);
            await db.SaveChangesAsync(ct);

            validId = valid.Id;
            wrongGradeId = wrongGrade.Id;
            wrongSeasonId = wrongSeason.Id;
            voidedId = voided.Id;
        }

        await using (var dbOther = factory.Postgres.CreateDbContext(TestCurrentUser.For(otherHousehold.Owner)))
        {
            var otherRec = new HarvestRecord { SeasonId = otherSeason.Id, HarvestDate = new DateOnly(2026, 10, 2), QualityGrade = QualityGrade.Grade1, Quantity = 100m, RequestId = Guid.CreateVersion7() };
            dbOther.HarvestRecords.Add(otherRec);
            await dbOther.SaveChangesAsync(ct);
            otherHouseId = otherRec.Id;
        }

        await using var verifyDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var service = new ProduceStockService(verifyDb, TestCurrentUser.For(household.Owner));

        // Rỗng hoặc trùng ID hợp lệ -> không lỗi
        await service.ValidateHarvestLinksForSaleAsync(season.Id, QualityGrade.Grade1, [], ct);
        await service.ValidateHarvestLinksForSaleAsync(season.Id, QualityGrade.Grade1, [validId, validId], ct);

        // Chứa các ID không hợp lệ -> VALIDATION_ERROR kèm invalidHarvestRecordIds
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            service.ValidateHarvestLinksForSaleAsync(
                season.Id,
                QualityGrade.Grade1,
                [validId, wrongGradeId, wrongSeasonId, voidedId, otherHouseId],
                ct));
        Assert.Equal(ErrorCodes.ValidationError, ex.Code);
        Assert.NotNull(ex.Details);
    }

    [Fact(DisplayName = "TC-15 & BR-SEA-05: EnsureZeroStockAsync chặn khi còn tồn và thông qua khi tồn = 0")]
    public async Task EnsureZeroStock_EnforcesZeroAcrossAllGrades()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            var service = new ProduceStockService(db, TestCurrentUser.For(household.Owner));
            // Mùa vụ chưa thu hoạch gì (mất trắng) -> hợp lệ
            await service.EnsureZeroStockAsync(season.Id, ct);

            db.HarvestRecords.Add(new HarvestRecord
            {
                SeasonId = season.Id,
                HarvestDate = new DateOnly(2026, 10, 3),
                QualityGrade = QualityGrade.Grade1,
                Quantity = 100m,
                RequestId = Guid.CreateVersion7(),
            });
            await db.SaveChangesAsync(ct);

            var ex = await Assert.ThrowsAsync<AppException>(() => service.EnsureZeroStockAsync(season.Id, ct));
            Assert.Equal(ErrorCodes.SeasonProduceRemaining, ex.Code);
            Assert.NotNull(ex.Details);
        }
    }
}
