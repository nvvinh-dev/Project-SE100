using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Seasons;

/// <summary>
/// Kiểm thử điểm nối kiểm tra trạng thái và phạm vi mùa vụ (NOI-03, BR-SEA-10, BR-USR-13, BR-GEN-02, BR-GEN-12).
/// </summary>
public class SeasonStateGuardTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "BR-GEN-02: mùa vụ không tồn tại hoặc thuộc hộ khác thì trả NotFound")]
    public async Task EnsureStatus_NotFoundOrOtherHousehold_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await TestData.CreateHouseholdAsync(postgres, ct);
        var householdB = await TestData.CreateHouseholdAsync(postgres, ct);
        var plotB = await TestData.CreatePlotAsync(postgres, householdB, "TD-B", ct);
        var seasonB = await TestData.CreateSeasonAsync(postgres, householdB, plotB, SeasonStatus.InProgress, ct);

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(householdA.Owner));
        var guard = new SeasonStateGuard(db, TestCurrentUser.For(householdA.Owner));

        // Mùa vụ không tồn tại
        var exNotFound = await Assert.ThrowsAsync<AppException>(
            () => guard.EnsureInProgressAsync(Guid.CreateVersion7(), null, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.NotFound, exNotFound.Code);

        // Mùa vụ thuộc hộ khác
        var exOther = await Assert.ThrowsAsync<AppException>(
            () => guard.EnsureInProgressAsync(seasonB.Id, null, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.NotFound, exOther.Code);
    }

    [Fact(DisplayName = "BR-GEN-02: Thành viên không được phân công mùa vụ thì coi như không tồn tại (NotFound)")]
    public async Task EnsureStatus_MemberWithoutAssignment_ThrowsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD-01", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.InProgress, ct);

        // Thành viên không có phân công nào
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Member));
        var guard = new SeasonStateGuard(db, TestCurrentUser.For(household.Member));

        var ex = await Assert.ThrowsAsync<AppException>(
            () => guard.EnsureInProgressAsync(season.Id, null, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    [Fact(DisplayName = "BR-USR-13: Thành viên có quyền Quản lý tài chính thao tác được với mùa vụ ngoài phân công khi ghi bán hàng")]
    public async Task EnsureStatus_FinancePermission_CanAccessOutOfScopeSeasonForSales()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD-01", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.InProgress, ct);

        // Thành viên có TC (CanManageFinance = true) nhưng không được phân công vào season
        var userWithFinance = household.MemberWithAllPermissions;
        await using var dbWithFinance = postgres.CreateDbContext(TestCurrentUser.For(userWithFinance));
        var guardWithFinance = new SeasonStateGuard(dbWithFinance, TestCurrentUser.For(userWithFinance));

        // Khi có outOfScopePermission = ManageFinance (bán hàng, thanh toán, chi phí) thì thành công
        var result = await guardWithFinance.EnsureOperationAllowedAsync(season.Id, SeasonOperation.ManageSales, forUpdate: false, ct);
        Assert.Equal(season.Id, result.Id);
        Assert.Equal(SeasonStatus.InProgress, result.Status);

        // Thành viên không có quyền TC thao tác với mùa vụ ngoài phân công thì bị NotFound
        await using var dbNoFinance = postgres.CreateDbContext(TestCurrentUser.For(household.Member));
        var guardNoFinance = new SeasonStateGuard(dbNoFinance, TestCurrentUser.For(household.Member));

        var ex = await Assert.ThrowsAsync<AppException>(
            () => guardNoFinance.EnsureOperationAllowedAsync(season.Id, SeasonOperation.ManageSales, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    [Fact(DisplayName = "BR-USR-13: Thành viên có quyền Quản lý kho thao tác được với mùa vụ ngoài phân công khi xuất kho")]
    public async Task EnsureStatus_StockPermission_CanAccessOutOfScopeSeasonForStockOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD-01", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.InProgress, ct);

        // Thành viên có Kho (CanManageStock = true) nhưng không phân công mùa vụ
        var userWithStock = household.MemberWithAllPermissions;
        await using var dbWithStock = postgres.CreateDbContext(TestCurrentUser.For(userWithStock));
        var guardWithStock = new SeasonStateGuard(dbWithStock, TestCurrentUser.For(userWithStock));

        var result = await guardWithStock.EnsureOperationAllowedAsync(season.Id, SeasonOperation.StockOut, forUpdate: false, ct);
        Assert.Equal(season.Id, result.Id);

        // Thành viên không có quyền Kho thì bị NotFound
        await using var dbNoStock = postgres.CreateDbContext(TestCurrentUser.For(household.Member));
        var guardNoStock = new SeasonStateGuard(dbNoStock, TestCurrentUser.For(household.Member));

        var ex = await Assert.ThrowsAsync<AppException>(
            () => guardNoStock.EnsureOperationAllowedAsync(season.Id, SeasonOperation.StockOut, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    [Fact(DisplayName = "BR-USR-13: Thành viên có quyền Quản lý tài chính tạo tóm tắt AI được với mùa vụ Closed ngoài phân công")]
    public async Task EnsureStatus_FinancePermission_CanAccessOutOfScopeSeasonForAiSummary()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD-01", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.Closed, ct);

        // Thành viên có TC (CanManageFinance = true) nhưng không phân công mùa vụ
        var userWithFinance = household.MemberWithAllPermissions;
        await using var dbWithFinance = postgres.CreateDbContext(TestCurrentUser.For(userWithFinance));
        var guardWithFinance = new SeasonStateGuard(dbWithFinance, TestCurrentUser.For(userWithFinance));

        var result = await guardWithFinance.EnsureOperationAllowedAsync(season.Id, SeasonOperation.GenerateAiSummary, forUpdate: false, ct);
        Assert.Equal(season.Id, result.Id);
        Assert.Equal(SeasonStatus.Closed, result.Status);

        // Thành viên không có quyền TC thì bị NotFound
        await using var dbNoFinance = postgres.CreateDbContext(TestCurrentUser.For(household.Member));
        var guardNoFinance = new SeasonStateGuard(dbNoFinance, TestCurrentUser.For(household.Member));

        var ex = await Assert.ThrowsAsync<AppException>(
            () => guardNoFinance.EnsureOperationAllowedAsync(season.Id, SeasonOperation.GenerateAiSummary, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.NotFound, ex.Code);
    }

    [Theory(DisplayName = "BR-SEA-10: kiểm tra các dòng trong bảng 4.3 ở trạng thái được phép")]
    [InlineData(SeasonOperation.EditScheduleAndYield, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.EditScheduleAndYield, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ChangePlotOrCrop, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.ManagePlannedSupplies, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.ManageAssignments, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.ManageTasks, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.RecordTaskExecution, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.StockOut, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ManageHarvests, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.AdjustProduceStock, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ManageSales, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.RecordPayment, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.RecordPayment, SeasonStatus.Closed)]
    [InlineData(SeasonOperation.ManageExpenses, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ManageNotes, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.ManageNotes, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ManageNotes, SeasonStatus.Closed)]
    [InlineData(SeasonOperation.ManageNotes, SeasonStatus.Cancelled)]
    [InlineData(SeasonOperation.GenerateAiSummary, SeasonStatus.Closed)]
    public async Task Table43_AllowedStatuses_Succeeds(SeasonOperation operation, SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, $"TD-{Guid.CreateVersion7():N}"[..8], ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, status, ct);

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var guard = new SeasonStateGuard(db, TestCurrentUser.For(household.Owner));

        var result = await guard.EnsureOperationAllowedAsync(season.Id, operation, forUpdate: false, ct);
        Assert.Equal(season.Id, result.Id);
        Assert.Equal(status, result.Status);
    }

    [Theory(DisplayName = "BR-SEA-10: kiểm tra các dòng trong bảng 4.3 ở trạng thái bị chặn trả về SEASON_INVALID_STATE")]
    [InlineData(SeasonOperation.EditScheduleAndYield, SeasonStatus.Closed)]
    [InlineData(SeasonOperation.ChangePlotOrCrop, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ManagePlannedSupplies, SeasonStatus.InProgress)]
    [InlineData(SeasonOperation.ManageAssignments, SeasonStatus.Closed)]
    [InlineData(SeasonOperation.ManageTasks, SeasonStatus.Cancelled)]
    [InlineData(SeasonOperation.RecordTaskExecution, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.StockOut, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.ManageHarvests, SeasonStatus.Closed)]
    [InlineData(SeasonOperation.AdjustProduceStock, SeasonStatus.Cancelled)]
    [InlineData(SeasonOperation.ManageSales, SeasonStatus.Planned)]
    [InlineData(SeasonOperation.RecordPayment, SeasonStatus.Cancelled)]
    [InlineData(SeasonOperation.ManageExpenses, SeasonStatus.Closed)]
    [InlineData(SeasonOperation.GenerateAiSummary, SeasonStatus.InProgress)]
    public async Task Table43_BlockedStatuses_ThrowsSeasonInvalidState(SeasonOperation operation, SeasonStatus status)
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, $"TD-{Guid.CreateVersion7():N}"[..8], ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, status, ct);

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var guard = new SeasonStateGuard(db, TestCurrentUser.For(household.Owner));

        var ex = await Assert.ThrowsAsync<AppException>(
            () => guard.EnsureOperationAllowedAsync(season.Id, operation, forUpdate: false, ct));
        Assert.Equal(ErrorCodes.SeasonInvalidState, ex.Code);
        Assert.Contains("không thể thực hiện thao tác này", ex.Message);
    }

    [Fact(DisplayName = "BR-GEN-12: yêu cầu khóa dòng khi chưa mở transaction ném InvalidOperationException")]
    public async Task EnsureStatus_ForUpdateWithoutTransaction_ThrowsInvalidOperationException()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD-01", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.InProgress, ct);

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var guard = new SeasonStateGuard(db, TestCurrentUser.For(household.Owner));

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => guard.EnsureInProgressAsync(season.Id, null, forUpdate: true, ct));
    }

    [Fact(DisplayName = "BR-GEN-12: khóa dòng seasons thành công trong transaction đang mở")]
    public async Task EnsureStatus_ForUpdateWithTransaction_LocksRowSuccessfully()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD-01", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.InProgress, ct);

        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var guard = new SeasonStateGuard(db, TestCurrentUser.For(household.Owner));
        var result = await guard.EnsureInProgressAsync(season.Id, null, forUpdate: true, ct);

        Assert.Equal(season.Id, result.Id);
        Assert.Equal(SeasonStatus.InProgress, result.Status);

        await tx.CommitAsync(ct);
    }
}

