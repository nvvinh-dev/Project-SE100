using System.Net;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.FarmTasks;

public sealed class FarmTaskReminderTests(PostgresFixture postgres) : IClassFixture<PostgresFixture>
{
    private const string RemindersPath = "/api/farm-tasks/reminders";

    [Fact(DisplayName = "FR-TSK-08, BR-TSK-12: Nhắc việc chia đúng Trễ hạn, Hôm nay, Sắp tới, loại việc Done/Cancelled và lọc theo phạm vi/mine")]
    public async Task GetReminders_ClassifiesBucketsAndRespectsScope()
    {
        var ct = TestContext.Current.CancellationToken;
        // 2026-10-10 10:00Z = 2026-10-10 17:00 VN -> Hôm nay là 2026-10-10
        await using var factory = new FarmTaskTestData.FixedClockFactory(
            postgres,
            new DateTimeOffset(2026, 10, 10, 10, 0, 0, TimeSpan.Zero));
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(postgres, household, "TD001", ct);
        var plot2 = await TestData.CreatePlotAsync(postgres, household, "TD002", ct);
        var seasonInScope = await TestData.CreateSeasonAsync(postgres, household, plot1, SeasonStatus.InProgress, ct);
        var seasonOutOfScope = await TestData.CreateSeasonAsync(postgres, household, plot2, SeasonStatus.InProgress, ct);

        await TestData.AssignMemberAsync(postgres, household, seasonInScope, household.Member, ct);
        await TestData.AssignMemberAsync(postgres, household, seasonInScope, household.Worker, ct);

        var overduePending = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 9), ct,
            assignee: household.Worker, status: FarmTaskStatus.Pending);
        var overdueWaiting = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 8), ct,
            assignee: household.Member, status: FarmTaskStatus.WaitingForSupply);
        var todayPending = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 10), ct,
            assignee: household.Owner, status: FarmTaskStatus.Pending);
        var upcomingBoundary = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 13), ct,
            assignee: household.Worker, status: FarmTaskStatus.Pending);
        var tooFarTask = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 14), ct,
            assignee: household.Worker, status: FarmTaskStatus.Pending);
        var doneTask = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 9), ct,
            assignee: household.Owner, status: FarmTaskStatus.Done);
        var cancelledTask = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonInScope, new DateOnly(2026, 10, 10), ct,
            assignee: household.Owner, status: FarmTaskStatus.Cancelled);
        var outOfScopeTask = await FarmTaskTestData.CreateTaskAsync(
            postgres, household, seasonOutOfScope, new DateOnly(2026, 10, 10), ct,
            assignee: household.Owner, status: FarmTaskStatus.Pending);

        var ownerClient = factory.CreateClientFor(household.Owner);
        var ownerRes = await ownerClient.GetAsync(RemindersPath, ct);
        Assert.Equal(HttpStatusCode.OK, ownerRes.StatusCode);

        var ownerData = (await ownerRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("2026-10-10", ownerData.GetProperty("asOfDate").GetString());
        Assert.Equal(3, ownerData.GetProperty("upcomingDays").GetInt32());

        var overdueIds = ownerData.GetProperty("overdue").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        var todayIds = ownerData.GetProperty("today").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();
        var upcomingIds = ownerData.GetProperty("upcoming").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList();

        Assert.Equal([overdueWaiting.Id, overduePending.Id], overdueIds);
        Assert.All(ownerData.GetProperty("overdue").EnumerateArray(), e => Assert.True(e.GetProperty("isOverdue").GetBoolean()));
        Assert.Equal([todayPending.Id, outOfScopeTask.Id], todayIds.OrderBy(id => id == todayPending.Id ? 0 : 1).ToList());
        Assert.Equal([upcomingBoundary.Id], upcomingIds);

        Assert.DoesNotContain(tooFarTask.Id, overdueIds.Concat(todayIds).Concat(upcomingIds));
        Assert.DoesNotContain(doneTask.Id, overdueIds.Concat(todayIds).Concat(upcomingIds));
        Assert.DoesNotContain(cancelledTask.Id, overdueIds.Concat(todayIds).Concat(upcomingIds));

        // Thành viên không thấy mùa vụ ngoài phân công, và lọc mine=true chỉ trả việc của chính mình
        var memberClient = factory.CreateClientFor(household.Member);
        var memberData = (await (await memberClient.GetAsync($"{RemindersPath}?mine=true", ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal([overdueWaiting.Id], memberData.GetProperty("overdue").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList());
        Assert.Equal(0, memberData.GetProperty("todayCount").GetInt32());
        Assert.Equal(0, memberData.GetProperty("upcomingCount").GetInt32());

        // Nhân công chỉ thấy công việc giao cho mình
        var workerClient = factory.CreateClientFor(household.Worker);
        var workerData = (await (await workerClient.GetAsync(RemindersPath, ct)).ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal([overduePending.Id], workerData.GetProperty("overdue").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList());
        Assert.Equal(0, workerData.GetProperty("todayCount").GetInt32());
        Assert.Equal([upcomingBoundary.Id], workerData.GetProperty("upcoming").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList());
    }

    [Fact(DisplayName = "BR-TSK-12: Mốc hôm nay tính theo giờ Việt Nam và đọc số ngày sắp tới từ cấu hình Reminders:UpcomingDays")]
    public async Task GetReminders_UsesVietnamClockBoundaryAndConfigUpcomingDays()
    {
        var ct = TestContext.Current.CancellationToken;
        // 2026-10-10 17:30Z = 2026-10-11 00:30 UTC+7 -> Hôm nay ở Việt Nam đã sang 2026-10-11 dù UTC vẫn là ngày 10
        await using var customFactory = new FarmTaskTestData.FixedClockFactory(
            postgres,
            new DateTimeOffset(2026, 10, 10, 17, 30, 0, TimeSpan.Zero),
            upcomingDays: 5);

        var household = await customFactory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(postgres, household, plot, SeasonStatus.InProgress, ct);

        var oct10Task = await FarmTaskTestData.CreateTaskAsync(postgres, household, season, new DateOnly(2026, 10, 10), ct);
        var oct11Task = await FarmTaskTestData.CreateTaskAsync(postgres, household, season, new DateOnly(2026, 10, 11), ct);
        var oct16Task = await FarmTaskTestData.CreateTaskAsync(postgres, household, season, new DateOnly(2026, 10, 16), ct);

        var ownerClient = customFactory.CreateClientFor(household.Owner);
        var res = await ownerClient.GetAsync(RemindersPath, ct);
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        var data = (await res.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("2026-10-11", data.GetProperty("asOfDate").GetString());
        Assert.Equal(5, data.GetProperty("upcomingDays").GetInt32());
        Assert.Equal([oct10Task.Id], data.GetProperty("overdue").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList());
        Assert.Equal([oct11Task.Id], data.GetProperty("today").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList());
        Assert.Equal([oct16Task.Id], data.GetProperty("upcoming").EnumerateArray().Select(e => e.GetProperty("id").GetGuid()).ToList());
    }
}
