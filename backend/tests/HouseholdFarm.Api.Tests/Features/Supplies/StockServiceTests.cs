using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Điểm nối kho cho mùa vụ và công việc (FR-SUP-10), gọi thẳng service như module khác gọi.</summary>
public class StockServiceTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateTimeOffset _now = new(2026, 11, 6, 3, 0, 0, TimeSpan.Zero);

    [Fact(DisplayName = "FR-SEA-04: lấy tồn và ngưỡng của nhiều vật tư; id trùng một dòng; danh sách rỗng trả rỗng")]
    public async Task GetStock_ReturnsStockAndMinimum()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 100m, unitCost: 1000m, minStockLevel: 30m);
        var second = await SupplyTestData.CreateSupplyAsync(factory.Postgres, arranged.Household, "VT002", ct);
        await using var scope = ScopeFor(arranged.Household.Member, []);

        var rows = await scope.Service.GetStockAsync([arranged.Supply.Id, second.Id, second.Id], ct);
        var empty = await scope.Service.GetStockAsync([], ct);

        Assert.Equal(2, rows.Count);
        var row = rows.Single(r => r.SupplyId == arranged.Supply.Id);
        Assert.Equal(100m, row.StockQuantity);
        Assert.Equal(30m, row.MinStockLevel);
        Assert.Empty(empty);
    }

    [Fact(DisplayName = "FR-SEA-04: Nhân công lấy tồn trả FORBIDDEN; vật tư hộ khác trả NOT_FOUND")]
    public async Task GetStock_Rejections()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 1m, unitCost: 1000m);
        var other = await factory.CreateHouseholdAsync(ct);
        var otherSupply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, other, "VT001", ct);
        await using var worker = ScopeFor(arranged.Household.Worker, []);
        await using var owner = ScopeFor(arranged.Household.Owner, []);

        var forbidden = await Assert.ThrowsAsync<AppException>(() => worker.Service.GetStockAsync([arranged.Supply.Id], ct));
        var notFound = await Assert.ThrowsAsync<AppException>(() => owner.Service.GetStockAsync([arranged.Supply.Id, otherSupply.Id], ct));

        Assert.Equal(ErrorCodes.Forbidden, forbidden.Code);
        Assert.Equal(ErrorCodes.NotFound, notFound.Code);
    }

    [Fact(DisplayName = "TC-04: xuất qua service: tồn 100 kg, ngưỡng 30 kg, dùng 80 kg còn 20 kg, cảnh báo LOW_STOCK, có nhật ký")]
    public async Task TC04_StockOutForFarmTask_WarnsLowStock()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 100m, unitCost: 1000m, minStockLevel: 30m);
        await using var scope = ScopeFor(arranged.Household.Owner, []);

        var result = await scope.Service.StockOutForFarmTaskAsync(
            arranged.FarmTask.Id, arranged.Supply.Id, 80m, performedDate: null, ct);
        await scope.Db.SaveChangesAsync(ct);

        Assert.Equal(StockTransactionType.Out, result.Type);
        Assert.Equal(-80m, result.Quantity);
        Assert.Equal(arranged.Season.Id, result.SeasonId);
        Assert.Equal(1000m, result.UnitCost);
        Assert.Equal([WarningCodes.LowStock], scope.Warnings.Warnings.Select(w => w.Code));
        Assert.Equal(20m, await StockAsync(arranged, ct));

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == result.Id, ct);
        Assert.Equal(AuditActions.StockTransactionStockOut, log.Action);
    }

    [Fact(DisplayName = "BR-TSK-08: gọi trong giao dịch của bên gọi rồi rollback thì không còn giao dịch kho")]
    public async Task StockOut_InCallerTransaction_RollsBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);
        await using var scope = ScopeFor(arranged.Household.Owner, []);

        await using (var callerTransaction = await scope.Db.Database.BeginTransactionAsync(ct))
        {
            await scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 4m, null, ct);
            // Lần gọi thứ hai trong cùng giao dịch phải tính trên tồn của lần trước.
            await scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 5m, null, ct);
            await Assert.ThrowsAsync<AppException>(
                () => scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 2m, null, ct));
            await callerTransaction.RollbackAsync(ct);
        }

        Assert.Equal(10m, await StockAsync(arranged, ct));
    }

    [Fact(DisplayName = "Chú thích (6): Thành viên không có Kho, được phân công, ghi vật tư của công việc vẫn xuất được")]
    public async Task StockOut_MemberWithoutManageStock_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);
        await TestData.AssignMemberAsync(factory.Postgres, arranged.Household, arranged.Season, arranged.Household.Member, ct);
        await using var scope = ScopeFor(arranged.Household.Member, [arranged.Season.Id]);

        var result = await scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 3m, null, ct);

        Assert.Equal(-3m, result.Quantity);
        Assert.Equal(7m, await StockAsync(arranged, ct));
    }

    [Fact(DisplayName = "BR-TSK-08: transaction_date lấy ngày thực hiện nếu có, không thì hôm nay theo giờ Việt Nam")]
    public async Task StockOut_TransactionDate()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m);
        await using var scope = ScopeFor(arranged.Household.Owner, []);

        var withDate = await scope.Service.StockOutForFarmTaskAsync(
            arranged.FarmTask.Id, arranged.Supply.Id, 1m, new DateOnly(2026, 10, 20), ct);
        var withoutDate = await scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 1m, null, ct);

        Assert.Equal(new DateOnly(2026, 10, 20), withDate.TransactionDate);
        Assert.Equal(new DateOnly(2026, 11, 6), withoutDate.TransactionDate);
    }

    [Fact(DisplayName = "BR-GEN-13: vật tư Inactive vẫn xuất được cho dòng vật tư đã có trong công việc")]
    public async Task StockOut_InactiveSupply_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 10m, unitCost: 1000m, status: RecordStatus.Inactive);
        await using var scope = ScopeFor(arranged.Household.Owner, []);

        var result = await scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 1m, null, ct);

        Assert.Equal(-1m, result.Quantity);
    }

    [Fact(DisplayName = "FR-SUP-10: thiếu tồn trả INSUFFICIENT_STOCK; công việc hộ khác trả NOT_FOUND; mùa vụ không InProgress trả SEASON_INVALID_STATE")]
    public async Task StockOut_Rejections()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 1m, unitCost: 1000m);
        var other = await ArrangeAsync(ct, stock: 1m, unitCost: 1000m);
        await using (var insufficient = ScopeFor(arranged.Household.Owner, []))
        {
            var error = await Assert.ThrowsAsync<AppException>(
                () => insufficient.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 2m, null, ct));
            Assert.Equal(ErrorCodes.InsufficientStock, error.Code);
        }

        await using (var foreignTask = ScopeFor(arranged.Household.Owner, []))
        {
            var error = await Assert.ThrowsAsync<AppException>(
                () => foreignTask.Service.StockOutForFarmTaskAsync(other.FarmTask.Id, arranged.Supply.Id, 1m, null, ct));
            Assert.Equal(ErrorCodes.NotFound, error.Code);
        }

        await CloseSeasonAsync(arranged, ct);
        await using var closed = ScopeFor(arranged.Household.Owner, []);
        var closedError = await Assert.ThrowsAsync<AppException>(
            () => closed.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, 1m, null, ct));
        Assert.Equal(ErrorCodes.SeasonInvalidState, closedError.Code);
    }

    [Fact(DisplayName = "BR-TSK-10, BR-SUP-11: hủy giao dịch xuất của công việc nhập lại theo đơn giá cũ, cập nhật giá bình quân, có nhật ký")]
    public async Task VoidFarmTaskStockOut_ReceivesBack()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 20m, unitCost: 22000m);
        var stockOutId = await StockOutWithUsageAsync(arranged, 15m, ct);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, arranged.Household, arranged.Supply, StockTransactionType.In, 5m, ct, unitCost: 30000m);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(arranged.Household.Owner)))
        {
            (await db.Supplies.SingleAsync(s => s.Id == arranged.Supply.Id, ct)).AverageUnitCost = 26000m;
            await db.SaveChangesAsync(ct);
        }

        await using var scope = ScopeFor(arranged.Household.Owner, []);
        await scope.Service.VoidFarmTaskStockOutAsync(stockOutId, " Sửa lượng ", ct);

        await using var check = factory.Postgres.CreateDbContext();
        var supply = await check.Supplies.IgnoreQueryFilters().SingleAsync(s => s.Id == arranged.Supply.Id, ct);
        Assert.Equal(23600m, supply.AverageUnitCost);
        Assert.Equal(25m, await StockAsync(arranged, ct));
        var transaction = await check.StockTransactions.IgnoreQueryFilters().SingleAsync(t => t.Id == stockOutId, ct);
        Assert.Equal("Sửa lượng", transaction.VoidReason);
        var log = await check.AuditLogs.IgnoreQueryFilters().SingleAsync(
            l => l.EntityId == stockOutId && l.Action == AuditActions.StockTransactionVoid,
            ct);
        Assert.Equal("Sửa lượng", log.Reason);
    }

    [Fact(DisplayName = "BR-TSK-11: hàm hủy của công việc từ chối giao dịch không gắn công việc; lý do rỗng, hủy lần hai bị từ chối")]
    public async Task VoidFarmTaskStockOut_Rejections()
    {
        var ct = TestContext.Current.CancellationToken;
        var arranged = await ArrangeAsync(ct, stock: 20m, unitCost: 1000m);
        var manual = await SupplyTestData.CreateTransactionAsync(
            factory.Postgres, arranged.Household, arranged.Supply, StockTransactionType.Out, -1m, ct, seasonId: arranged.Season.Id);
        var stockOutId = await StockOutWithUsageAsync(arranged, 2m, ct);

        await using var scope = ScopeFor(arranged.Household.Owner, []);
        var notLinked = await Assert.ThrowsAsync<AppException>(() => scope.Service.VoidFarmTaskStockOutAsync(manual.Id, "x", ct));
        var blank = await Assert.ThrowsAsync<AppException>(() => scope.Service.VoidFarmTaskStockOutAsync(stockOutId, "  ", ct));
        await scope.Service.VoidFarmTaskStockOutAsync(stockOutId, "x", ct);
        var twice = await Assert.ThrowsAsync<AppException>(() => scope.Service.VoidFarmTaskStockOutAsync(stockOutId, "x", ct));

        Assert.Equal(ErrorCodes.NotFound, notLinked.Code);
        Assert.Equal(ErrorCodes.ValidationError, blank.Code);
        Assert.Equal(ErrorCodes.AlreadyVoided, twice.Code);
    }

    private async Task<Guid> StockOutWithUsageAsync(Arranged arranged, decimal quantity, CancellationToken ct)
    {
        await using var scope = ScopeFor(arranged.Household.Owner, []);
        var result = await scope.Service.StockOutForFarmTaskAsync(arranged.FarmTask.Id, arranged.Supply.Id, quantity, null, ct);
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(arranged.Household.Owner));
        db.FarmTaskSupplyUsages.Add(new FarmTaskSupplyUsage
        {
            FarmTaskId = arranged.FarmTask.Id,
            SupplyId = arranged.Supply.Id,
            ActualQuantity = quantity,
            StockTransactionId = result.Id,
        });
        await db.SaveChangesAsync(ct);
        return result.Id;
    }

    private async Task CloseSeasonAsync(Arranged arranged, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(arranged.Household.Owner));
        (await db.Seasons.SingleAsync(s => s.Id == arranged.Season.Id, ct)).Status = SeasonStatus.Closed;
        await db.SaveChangesAsync(ct);
    }

    private async Task<decimal> StockAsync(Arranged arranged, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(arranged.Household.Owner));
        return await db.StockTransactions.StockQuantityOfAsync(arranged.Supply.Id, ct);
    }

    private async Task<Arranged> ArrangeAsync(
        CancellationToken ct,
        decimal stock,
        decimal unitCost,
        decimal minStockLevel = 0m,
        RecordStatus status = RecordStatus.Active)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, minStockLevel: minStockLevel, averageUnitCost: unitCost, status: status);
        await SupplyTestData.CreateTransactionAsync(factory.Postgres, household, supply, StockTransactionType.In, stock, ct, unitCost: unitCost);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct, taskAssignee: household.Member);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var farmTask = await db.FarmTasks.AsNoTracking().SingleAsync(t => t.SeasonId == season.Id, ct);
        return new Arranged(household, supply, season, farmTask);
    }

    // Dựng service như DI của ứng dụng, với người dùng hiện tại truyền vào thay cho đăng nhập.
    private ServiceScope ScopeFor(ApplicationUser user, Guid[] assignedSeasonIds)
    {
        var currentUser = TestCurrentUser.For(user, assignedSeasonIds);
        var db = factory.Postgres.CreateDbContext(currentUser);
        var clock = TestClock.At(_now);
        var warnings = new WarningCollector();
        var audit = new AuditLogger(db, currentUser, clock, Options.Create(new JsonOptions()));
        var ledger = new StockLedger(db, warnings);
        var operations = new StockOperations(db, currentUser, clock, audit, ledger);
        var service = new StockService(db, currentUser, clock, new SeasonStateGuard(db, currentUser), ledger, operations);
        return new ServiceScope(db, service, warnings);
    }

    private sealed record Arranged(TestHousehold Household, Supply Supply, Season Season, FarmTask FarmTask);

    private sealed record ServiceScope(AppDbContext Db, StockService Service, WarningCollector Warnings) : IAsyncDisposable
    {
        public ValueTask DisposeAsync() => Db.DisposeAsync();
    }
}
