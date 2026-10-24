using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Common.Persistence;

// Mỗi test tạo một hộ có hai mùa vụ: X phân công cho Thành viên (không quyền) và Nhân công, Y không phân công ai.
// Thành viên đủ quyền không được phân công mùa vụ nào, để thấy rõ phần mở rộng của quyền Kho, TC (BR-USR-13).
public class WorkScopeQueryExtensionsTests(PostgresFixture postgres)
{
    [Fact(DisplayName = "Chủ hộ thấy dữ liệu của mọi mùa vụ trong hộ")]
    public async Task VisibleTo_Owner_SeesWholeHousehold()
    {
        var data = await SeedAsync();

        var visible = await QueryAllAsync(TestCurrentUser.For(data.Household.Owner));

        Assert.Equal(Sorted(data.X.Season, data.Y.Season), visible.Seasons);
        Assert.Equal(Sorted(data.X.WorkerTask, data.X.MemberTask, data.Y.WorkerTask), visible.FarmTasks);
        Assert.Equal(Sorted(data.X.Usage, data.Y.Usage), visible.Usages);
        Assert.Equal(Sorted(data.X.Note, data.Y.Note), visible.Notes);
        Assert.Equal(Sorted(data.X.Plan, data.Y.Plan), visible.Plans);
        Assert.Equal(Sorted(data.X.MemberAssignment, data.X.WorkerAssignment), visible.Assignments);
        Assert.Equal(Sorted(data.X.Harvest, data.Y.Harvest), visible.Harvests);
        Assert.Equal(Sorted(data.X.Adjustment, data.Y.Adjustment), visible.Adjustments);
    }

    [Fact(DisplayName = "BR-GEN-02: Thành viên hộ chỉ thấy dữ liệu của mùa vụ được phân công, kể cả công việc của người khác trong đó")]
    public async Task VisibleTo_Member_SeesOnlyAssignedSeasons()
    {
        var data = await SeedAsync();

        var visible = await QueryAllAsync(TestCurrentUser.For(data.Household.Member, data.X.Season));

        Assert.Equal([data.X.Season], visible.Seasons);
        Assert.Equal(Sorted(data.X.WorkerTask, data.X.MemberTask), visible.FarmTasks);
        Assert.Equal([data.X.Usage], visible.Usages);
        Assert.Equal([data.X.Note], visible.Notes);
        Assert.Equal([data.X.Plan], visible.Plans);
        Assert.Equal(Sorted(data.X.MemberAssignment, data.X.WorkerAssignment), visible.Assignments);
        Assert.Equal([data.X.Harvest], visible.Harvests);
        Assert.Equal([data.X.Adjustment], visible.Adjustments);
    }

    [Fact(DisplayName = "BR-USR-14: Nhân công chỉ thấy công việc giao cho mình và vật tư của công việc đó, không thấy mùa vụ")]
    public async Task VisibleTo_Worker_SeesOnlyOwnFarmTasks()
    {
        var data = await SeedAsync();

        var visible = await QueryAllAsync(TestCurrentUser.For(data.Household.Worker, data.X.Season));

        // Công việc ở Y vẫn thấy dù Nhân công không còn được phân công Y: phạm vi theo người được giao.
        Assert.Equal(Sorted(data.X.WorkerTask, data.Y.WorkerTask), visible.FarmTasks);
        Assert.Equal(Sorted(data.X.Usage, data.Y.Usage), visible.Usages);
        Assert.Empty(visible.Seasons);
        Assert.Empty(visible.Notes);
        Assert.Empty(visible.Plans);
        Assert.Empty(visible.Assignments);
        Assert.Empty(visible.Harvests);
        Assert.Empty(visible.Adjustments);
    }

    [Fact(DisplayName = "Quản trị hệ thống và người chưa đăng nhập không thấy dữ liệu mùa vụ nào, kể cả khi bỏ lọc theo hộ")]
    public async Task VisibleTo_SystemAdminOrAnonymous_SeesNothing()
    {
        var data = await SeedAsync();
        await using var db = postgres.CreateDbContext(TestCurrentUser.Owner(data.Household.Household.Id));

        foreach (var user in new[] { TestCurrentUser.SystemAdmin(), TestCurrentUser.Anonymous })
        {
            var seasons = db.Seasons.IgnoreQueryFilters([AppDbContext.HouseholdFilter]);
            var farmTasks = db.FarmTasks.IgnoreQueryFilters([AppDbContext.HouseholdFilter]);
            Assert.False(await seasons.VisibleTo(user).AnyAsync(TestContext.Current.CancellationToken));
            Assert.False(await farmTasks.VisibleTo(user).AnyAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact(DisplayName = "BR-USR-13: Thành viên có Kho, TC chọn được mọi mùa vụ; TC thấy thêm phiếu thu, điều chỉnh tồn nông sản")]
    public async Task VisibleTo_WidenedByStockOrFinance_SeesAllSeasonsButNotTasks()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await SeedAsync();
        var user = TestCurrentUser.For(data.Household.MemberWithAllPermissions);
        await using var db = postgres.CreateDbContext(user);

        Assert.Empty(await Ids(db.Seasons.VisibleTo(user), ct));
        Assert.Equal(
            Sorted(data.X.Season, data.Y.Season),
            await Ids(db.Seasons.VisibleTo(user, ExtraPermission.ManageStock), ct));
        Assert.Equal(
            Sorted(data.X.Season, data.Y.Season),
            await Ids(db.Seasons.VisibleTo(user, ExtraPermission.ManageFinance), ct));
        Assert.Equal(
            Sorted(data.X.Harvest, data.Y.Harvest),
            await Ids(db.HarvestRecords.VisibleTo(user, ExtraPermission.ManageFinance), ct));
        Assert.Equal(
            Sorted(data.X.Adjustment, data.Y.Adjustment),
            await Ids(db.ProduceStockAdjustments.VisibleTo(user, ExtraPermission.ManageFinance), ct));

        // Công việc, ghi chú, phân công của mùa vụ ngoài phạm vi vẫn không thấy.
        var visible = await QueryAllAsync(user);
        Assert.Empty(visible.FarmTasks);
        Assert.Empty(visible.Notes);
        Assert.Empty(visible.Assignments);
    }

    [Fact(DisplayName = "BR-USR-13: thiếu quyền mở rộng thì chỉ thấy mùa vụ được phân công; Kho không mở phiếu thu hoạch")]
    public async Task VisibleTo_WithoutWideningPermission_FallsBackToAssignedSeasons()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await SeedAsync();
        var member = TestCurrentUser.For(data.Household.Member, data.X.Season);
        var stockOnly = new TestCurrentUser
        {
            IsAuthenticated = true,
            UserId = data.Household.Member.Id,
            HouseholdId = data.Household.Household.Id,
            Role = UserRole.Member,
            CanManageStock = true,
        };
        await using var db = postgres.CreateDbContext(member);

        Assert.Equal([data.X.Season], await Ids(db.Seasons.VisibleTo(member, ExtraPermission.ManageFinance), ct));
        Assert.Equal([data.X.Harvest], await Ids(db.HarvestRecords.VisibleTo(member, ExtraPermission.ManageFinance), ct));
        Assert.Empty(await Ids(db.Seasons.VisibleTo(stockOnly, ExtraPermission.ManageFinance), ct));
        Assert.Equal(
            Sorted(data.X.Season, data.Y.Season),
            await Ids(db.Seasons.VisibleTo(stockOnly, ExtraPermission.ManageStock), ct));

        Assert.Throws<ArgumentOutOfRangeException>(() => db.HarvestRecords.VisibleTo(stockOnly, ExtraPermission.ManageStock));
        Assert.Throws<ArgumentOutOfRangeException>(() => db.Seasons.VisibleTo(member, ExtraPermission.CloseSeason));
    }

    private async Task<VisibleIds> QueryAllAsync(TestCurrentUser user)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var db = postgres.CreateDbContext(user);
        return new VisibleIds(
            await Ids(db.Seasons.VisibleTo(user), ct),
            await Ids(db.FarmTasks.VisibleTo(user), ct),
            await Ids(db.FarmTaskSupplyUsages.VisibleTo(user), ct),
            await Ids(db.SeasonNotes.VisibleTo(user), ct),
            await Ids(db.SeasonSupplyPlans.VisibleTo(user), ct),
            await Ids(db.SeasonAssignments.VisibleTo(user), ct),
            await Ids(db.HarvestRecords.VisibleTo(user), ct),
            await Ids(db.ProduceStockAdjustments.VisibleTo(user), ct));
    }

    private static async Task<List<Guid>> Ids<TEntity>(IQueryable<TEntity> query, CancellationToken ct)
        where TEntity : class =>
        (await query.Select(e => EF.Property<Guid>(e, "Id")).ToListAsync(ct)).Order().ToList();

    private static List<Guid> Sorted(params Guid[] ids) => ids.Order().ToList();

    private async Task<ScopeData> SeedAsync()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await TestData.CreateHouseholdAsync(postgres, ct);
        await using var db = postgres.CreateDbContext(TestCurrentUser.For(household.Owner));

        var plot = new Plot
        {
            Code = "TD001",
            AreaM2 = 1500m,
            SoilType = SoilType.Alluvial,
            WaterSource = WaterSource.RiverCanal,
            Status = RecordStatus.Active,
        };
        var crop = new Crop { Name = "Lúa ST25", Group = CropGroup.Staple, YieldUnit = Unit.Kilogram, Status = RecordStatus.Active };
        var supply = new Supply
        {
            Code = "VT001",
            Name = "Phân NPK",
            Group = SupplyGroup.Fertilizer,
            Unit = Unit.Kilogram,
            Status = RecordStatus.Active,
        };
        db.AddRange(plot, crop, supply);

        // Nhân công được giao một công việc đã xong ở Y, như khi đã bị thu hồi phân công Y (BR-USR-12).
        var x = AddSeason(db, plot, crop, supply, household, assigned: true);
        var y = AddSeason(db, plot, crop, supply, household, assigned: false);
        await db.SaveChangesAsync(ct);

        return new ScopeData(household, x, y);
    }

    private static SeasonData AddSeason(
        AppDbContext db,
        Plot plot,
        Crop crop,
        Supply supply,
        TestHousehold household,
        bool assigned)
    {
        var season = new Season
        {
            Plot = plot,
            Crop = crop,
            StartDate = new DateOnly(2026, 9, 1),
            ExpectedHarvestDate = new DateOnly(2026, 12, 1),
            TargetYield = 3000m,
            Status = SeasonStatus.InProgress,
        };
        var workerTask = NewFarmTask(season, household.Worker, assigned ? FarmTaskStatus.Pending : FarmTaskStatus.Done);
        var memberTask = assigned ? NewFarmTask(season, household.Member, FarmTaskStatus.Pending) : null;
        var usage = new FarmTaskSupplyUsage { FarmTask = workerTask, Supply = supply, PlannedQuantity = 5m };
        var note = new SeasonNote { Season = season, Content = "Bón lót trước khi gieo" };
        var plan = new SeasonSupplyPlan { Season = season, Supply = supply, PlannedQuantity = 50m };
        var harvest = new HarvestRecord
        {
            Season = season,
            HarvestDate = new DateOnly(2026, 10, 20),
            QualityGrade = QualityGrade.Grade1,
            Quantity = 500m,
            RequestId = Guid.CreateVersion7(),
        };
        var adjustment = new ProduceStockAdjustment
        {
            Season = season,
            QualityGrade = QualityGrade.Grade1,
            Quantity = 10m,
            AdjustmentDate = new DateOnly(2026, 10, 21),
            Reason = ProduceAdjustmentReason.HouseholdUse,
            RequestId = Guid.CreateVersion7(),
        };
        var memberAssignment = assigned ? new SeasonAssignment { Season = season, UserId = household.Member.Id } : null;
        var workerAssignment = assigned ? new SeasonAssignment { Season = season, UserId = household.Worker.Id } : null;

        db.AddRange(season, workerTask, usage, note, plan, harvest, adjustment);
        if (assigned)
        {
            db.AddRange(memberTask!, memberAssignment!, workerAssignment!);
        }

        return new SeasonData(
            season.Id,
            workerTask.Id,
            memberTask?.Id ?? Guid.Empty,
            usage.Id,
            note.Id,
            plan.Id,
            memberAssignment?.Id ?? Guid.Empty,
            workerAssignment?.Id ?? Guid.Empty,
            harvest.Id,
            adjustment.Id);
    }

    private static FarmTask NewFarmTask(Season season, ApplicationUser assignee, FarmTaskStatus status) => new()
    {
        Season = season,
        AssigneeId = assignee.Id,
        Type = FarmTaskType.Fertilizing,
        ScheduledDate = new DateOnly(2026, 10, 15),
        Status = status,
    };

    private sealed record ScopeData(TestHousehold Household, SeasonData X, SeasonData Y);

    // Id bằng Guid.Empty là bản ghi không được tạo ở mùa vụ đó.
    private sealed record SeasonData(
        Guid Season,
        Guid WorkerTask,
        Guid MemberTask,
        Guid Usage,
        Guid Note,
        Guid Plan,
        Guid MemberAssignment,
        Guid WorkerAssignment,
        Guid Harvest,
        Guid Adjustment);

    private sealed record VisibleIds(
        List<Guid> Seasons,
        List<Guid> FarmTasks,
        List<Guid> Usages,
        List<Guid> Notes,
        List<Guid> Plans,
        List<Guid> Assignments,
        List<Guid> Harvests,
        List<Guid> Adjustments);
}
