using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.FarmTasks;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.FarmTasks;

/// <summary>
/// Kiểm thử tạo, tạo lặp, sửa, hủy, hủy nhóm lặp và lọc danh sách công việc
/// (FR-TSK-01, FR-TSK-02, FR-TSK-03, FR-TSK-09, FR-TSK-11, FR-SEA-05).
/// </summary>
public class FarmTaskCreateEditCancelListTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string TasksPath = "/api/farm-tasks";

    [Fact(DisplayName = "FR-TSK-01, FR-SEA-05: Chủ hộ tạo công việc kèm vật tư dự kiến cho mùa vụ Planned và InProgress")]
    public async Task Create_ValidRequest_InPlannedAndInProgressSeasons_ReturnsCreated()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plannedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct, name: "Phân NPK");

        await TestData.AssignMemberAsync(factory.Postgres, household, plannedSeason, household.Worker, ct);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateFarmTaskRequest(
            plannedSeason.Id,
            FarmTaskType.Fertilizing,
            null,
            new DateOnly(2026, 10, 5),
            household.Worker.Id,
            90,
            "Bón lót đầu vụ",
            [new FarmTaskPlannedSupplyInput(supply.Id, 12.5m)]);

        var response = await client.PostAsJsonAsync(TasksPath, request, FarmTaskTestData.JsonOptions, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        var taskId = data.GetProperty("id").GetGuid();
        Assert.Equal(plannedSeason.Id, data.GetProperty("seasonId").GetGuid());
        Assert.Equal("Pending", data.GetProperty("status").GetString());
        Assert.Equal("Fertilizing", data.GetProperty("type").GetString());
        Assert.Equal(household.Worker.Id, data.GetProperty("assigneeId").GetGuid());
        Assert.Equal(90, data.GetProperty("plannedDurationMinutes").GetInt32());
        Assert.Single(data.GetProperty("supplyUsages").EnumerateArray());
        Assert.Equal(12.5m, data.GetProperty("supplyUsages")[0].GetProperty("plannedQuantity").GetDecimal());
        Assert.True(data.GetProperty("version").GetUInt32() > 0);
        Assert.Equal($"{TasksPath}/{taskId}", response.Headers.Location!.AbsolutePath);
    }

    [Fact(DisplayName = "BR-TSK-02: Giao cho người chưa phân công vào mùa vụ trả ASSIGNEE_NOT_IN_SEASON; Chủ hộ và Nhân công được phép")]
    public async Task Create_AssigneeValidation_EnforcesBrTsk02()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, householdA, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, householdA, plot, SeasonStatus.InProgress, ct);

        var client = factory.CreateClientFor(householdA.Owner);

        // Giao cho thành viên chưa phân công -> ASSIGNEE_NOT_IN_SEASON
        var unassignedReq = new CreateFarmTaskRequest(
            season.Id,
            FarmTaskType.Watering,
            null,
            new DateOnly(2026, 10, 10),
            householdA.Member.Id,
            null,
            null,
            null);
        var unassignedRes = await client.PostAsJsonAsync(TasksPath, unassignedReq, FarmTaskTestData.JsonOptions, ct);
        await unassignedRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AssigneeNotInSeason, ct);

        // Giao cho người hộ B -> ASSIGNEE_NOT_IN_SEASON
        var crossHouseholdReq = unassignedReq with { AssigneeId = householdB.Owner.Id };
        var crossRes = await client.PostAsJsonAsync(TasksPath, crossHouseholdReq, FarmTaskTestData.JsonOptions, ct);
        await crossRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.AssigneeNotInSeason, ct);

        // Giao cho chính Chủ hộ (không cần SeasonAssignment) -> Created
        var ownerReq = unassignedReq with { AssigneeId = householdA.Owner.Id };
        var ownerRes = await client.PostAsJsonAsync(TasksPath, ownerReq, FarmTaskTestData.JsonOptions, ct);
        Assert.Equal(HttpStatusCode.Created, ownerRes.StatusCode);

        // Nhân công tự tạo việc -> 403 FORBIDDEN
        var workerClient = factory.CreateClientFor(householdA.Worker);
        var workerRes = await workerClient.PostAsJsonAsync(
            TasksPath,
            ownerReq with { AssigneeId = null },
            FarmTaskTestData.JsonOptions,
            ct);
        await workerRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "BR-SEA-10, BR-GEN-13: Mùa vụ đã chốt hoặc vật tư ngừng sử dụng bị từ chối khi tạo việc")]
    public async Task Create_ClosedSeasonOrInactiveSupply_Rejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var closedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var activeSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var inactiveSupply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT002", ct, status: RecordStatus.Inactive);

        var client = factory.CreateClientFor(household.Owner);

        var closedRes = await client.PostAsJsonAsync(
            TasksPath,
            new CreateFarmTaskRequest(closedSeason.Id, FarmTaskType.Weeding, null, new DateOnly(2026, 10, 10), null, null, null, null),
            FarmTaskTestData.JsonOptions,
            ct);
        await closedRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);

        var inactiveSupplyRes = await client.PostAsJsonAsync(
            TasksPath,
            new CreateFarmTaskRequest(
                activeSeason.Id,
                FarmTaskType.Spraying,
                null,
                new DateOnly(2026, 10, 10),
                null,
                null,
                null,
                [new FarmTaskPlannedSupplyInput(inactiveSupply.Id, 2m)]),
            FarmTaskTestData.JsonOptions,
            ct);
        await inactiveSupplyRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.CatalogInactive, ct);
    }

    [Fact(DisplayName = "FR-TSK-02, BR-TSK-03: Tạo công việc lặp sinh đúng các ngày đến hết expected_harvest_date (gồm ngày cuối)")]
    public async Task CreateRecurring_GeneratesTasksUpToExpectedHarvestDateInclusive()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        // Season ExpectedHarvestDate mặc định là 2027-01-15
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);
        var supply = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);

        var client = factory.CreateClientFor(household.Owner);
        // 2027-01-01 + 7*k: 2027-01-01, 2027-01-08, 2027-01-15 (3 công việc, tính cả 15/01)
        var request = new CreateFarmTaskRequest(
            season.Id,
            FarmTaskType.Watering,
            null,
            new DateOnly(2027, 1, 1),
            household.Owner.Id,
            45,
            "Tưới định kỳ",
            [new FarmTaskPlannedSupplyInput(supply.Id, 5m)],
            RecurrenceIntervalDays: 7);

        var response = await client.PostAsJsonAsync(TasksPath, request, FarmTaskTestData.JsonOptions, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var items = (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(3, items.Count);
        Assert.Equal("2027-01-01", items[0].GetProperty("scheduledDate").GetString());
        Assert.Equal("2027-01-08", items[1].GetProperty("scheduledDate").GetString());
        Assert.Equal("2027-01-15", items[2].GetProperty("scheduledDate").GetString());

        var groupId = items[0].GetProperty("recurrenceGroupId").GetGuid();
        Assert.All(items, item =>
        {
            Assert.Equal(groupId, item.GetProperty("recurrenceGroupId").GetGuid());
            Assert.Single(item.GetProperty("supplyUsages").EnumerateArray());
        });

        // Ngày đầu > expected_harvest_date -> VALIDATION_ERROR
        var invalidRes = await client.PostAsJsonAsync(
            TasksPath,
            request with { ScheduledDate = new DateOnly(2027, 1, 16) },
            FarmTaskTestData.JsonOptions,
            ct);
        await invalidRes.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        // Chu kỳ 0 -> VALIDATION_ERROR
        var zeroIntervalRes = await client.PostAsJsonAsync(
            TasksPath,
            request with { RecurrenceIntervalDays = 0 },
            FarmTaskTestData.JsonOptions,
            ct);
        await zeroIntervalRes.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-TSK-03, BR-TSK-01, BR-GEN-11: Sửa và hủy công việc Pending, phân quyền Thành viên theo người tạo, mùa vụ đóng và xung đột version")]
    public async Task UpdateAndCancel_EnforceStatusAndConcurrency()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var closedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Closed, ct);
        var outOfScopeSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var supply1 = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT001", ct);
        var supply2 = await SupplyTestData.CreateSupplyAsync(factory.Postgres, household, "VT002", ct);

        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, closedSeason, household.Member, ct);

        // Công việc do Chủ hộ tạo
        var ownerPendingTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres,
            household,
            season,
            new DateOnly(2026, 10, 10),
            ct,
            createdBy: household.Member,
            supplies: (supply1, 4m, null, null));

        var ownerPendingTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres,
            household,
            season,
            new DateOnly(2026, 10, 11),
            ct,
            createdBy: household.Owner);

        var doneTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres,
            household,
            season,
            new DateOnly(2026, 10, 9),
            ct,
            createdBy: household.Member,
            status: FarmTaskStatus.Done);

        var closedSeasonTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres,
            household,
            closedSeason,
            new DateOnly(2026, 10, 8),
            ct);

        var outOfScopeTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres,
            household,
            outOfScopeSeason,
            new DateOnly(2026, 10, 8),
            ct);

        var ownerClient = factory.CreateClientFor(household.Owner);
        var memberClient = factory.CreateClientFor(household.Member);

        // Thành viên tạo công việc riêng -> có quyền tự sửa và hủy
        var memberCreateRes = await memberClient.PostAsJsonAsync(
            TasksPath,
            new CreateFarmTaskRequest(
                season.Id,
                FarmTaskType.Weeding,
                null,
                new DateOnly(2026, 10, 10),
                household.Member.Id,
                60,
                "Làm cỏ",
                [new FarmTaskPlannedSupplyInput(supply1.Id, 4m)]),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.Created, memberCreateRes.StatusCode);
        var memberTaskData = (await memberCreateRes.ReadJsonAsync(ct)).GetProperty("data");
        var memberTaskId = memberTaskData.GetProperty("id").GetGuid();
        var version = memberTaskData.GetProperty("version").GetUInt32();

        // Thành viên sửa/hủy việc do Chủ hộ tạo -> FORBIDDEN
        var forbiddenUpdate = await memberClient.PutAsJsonAsync(
            $"{TasksPath}/{ownerPendingTask.Id}",
            new UpdateFarmTaskRequest(
                FarmTaskType.Other,
                "Tỉa cành",
                new DateOnly(2026, 10, 12),
                household.Member.Id,
                120,
                null,
                null,
                ownerPendingTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await forbiddenUpdate.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        var forbiddenCancel = await memberClient.PostAsJsonAsync(
            $"{TasksPath}/{ownerPendingTask.Id}/cancel",
            new CancelFarmTaskRequest(ownerPendingTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await forbiddenCancel.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        // Thành viên ngoài phạm vi mùa vụ -> NOT_FOUND
        var notFoundRes = await memberClient.GetAsync($"{TasksPath}/{outOfScopeTask.Id}", ct);
        await notFoundRes.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);

        // Thành viên sửa công việc do mình tạo thành công
        var updateReq = new UpdateFarmTaskRequest(
            FarmTaskType.Other,
            "Tỉa cành",
            new DateOnly(2026, 10, 12),
            household.Member.Id,
            120,
            "Đã đổi lịch",
            [new FarmTaskPlannedSupplyInput(supply2.Id, 8m)],
            version);
        var updateRes = await memberClient.PutAsJsonAsync($"{TasksPath}/{memberTaskId}", updateReq, FarmTaskTestData.JsonOptions, ct);
        Assert.Equal(HttpStatusCode.OK, updateRes.StatusCode);

        var updatedData = (await updateRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Other", updatedData.GetProperty("type").GetString());
        Assert.Equal("Tỉa cành", updatedData.GetProperty("typeOther").GetString());
        Assert.Equal("2026-10-12", updatedData.GetProperty("scheduledDate").GetString());
        Assert.Single(updatedData.GetProperty("supplyUsages").EnumerateArray());
        Assert.Equal(supply2.Id, updatedData.GetProperty("supplyUsages")[0].GetProperty("supplyId").GetGuid());
        var newVersion = updatedData.GetProperty("version").GetUInt32();

        // Gửi lại với version cũ -> CONCURRENCY_CONFLICT
        var staleRes = await memberClient.PutAsJsonAsync($"{TasksPath}/{memberTaskId}", updateReq, FarmTaskTestData.JsonOptions, ct);
        await staleRes.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.ConcurrencyConflict, ct);

        // Hủy công việc với version mới -> Cancelled
        var cancelRes = await memberClient.PostAsJsonAsync(
            $"{TasksPath}/{memberTaskId}/cancel",
            new CancelFarmTaskRequest(newVersion),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, cancelRes.StatusCode);
        var cancelledData = (await cancelRes.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Cancelled", cancelledData.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, cancelledData.GetProperty("cancelledAt").ValueKind);

        // Thành viên không sửa được việc do Chủ hộ tạo -> FORBIDDEN
        var forbiddenEditRes = await memberClient.PutAsJsonAsync(
            $"{TasksPath}/{ownerPendingTask.Id}",
            updateReq with { Version = ownerPendingTask.Version },
            FarmTaskTestData.JsonOptions,
            ct);
        await forbiddenEditRes.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        // Sửa công việc đã Done -> TASK_INVALID_STATE
        var editDoneRes = await ownerClient.PutAsJsonAsync(
            $"{TasksPath}/{doneTask.Id}",
            updateReq with { Version = doneTask.Version },
            FarmTaskTestData.JsonOptions,
            ct);
        await editDoneRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.TaskInvalidState, ct);

        // Sửa, hủy khi mùa vụ Closed -> SEASON_INVALID_STATE
        var editClosedRes = await ownerClient.PutAsJsonAsync(
            $"{TasksPath}/{closedSeasonTask.Id}",
            updateReq with { Version = closedSeasonTask.Version },
            FarmTaskTestData.JsonOptions,
            ct);
        await editClosedRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);

        var cancelClosedRes = await ownerClient.PostAsJsonAsync(
            $"{TasksPath}/{closedSeasonTask.Id}/cancel",
            new CancelFarmTaskRequest(closedSeasonTask.Version),
            FarmTaskTestData.JsonOptions,
            ct);
        await cancelClosedRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "FR-TSK-11, BR-TSK-04: Hủy nhóm lặp chỉ hủy các việc Pending/WaitingForSupply, giữ nguyên việc Done")]
    public async Task CancelRecurrenceGroup_CancelsOnlyPendingAndWaiting_KeepsDone()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var groupId = Guid.CreateVersion7();

        var doneTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 5), ct,
            status: FarmTaskStatus.Done, recurrenceGroupId: groupId);
        var pendingTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 10), ct,
            status: FarmTaskStatus.Pending, recurrenceGroupId: groupId);
        var waitingTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 15), ct,
            status: FarmTaskStatus.WaitingForSupply, recurrenceGroupId: groupId);
        var singleTask = await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, season, new DateOnly(2026, 10, 16), ct);

        var client = factory.CreateClientFor(household.Owner);
        var response = await client.PostAsJsonAsync(
            $"{TasksPath}/{doneTask.Id}/cancel",
            new CancelFarmTaskRequest(doneTask.Version, IncludeRecurrenceGroup: true),
            FarmTaskTestData.JsonOptions,
            ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(groupId, data.GetProperty("recurrenceGroupId").GetGuid());
        Assert.Equal(2, data.GetProperty("cancelledCount").GetInt32());

        // Hủy nhóm lặp từ việc không thuộc nhóm lặp -> TASK_INVALID_STATE
        var nonGroupRes = await client.PostAsJsonAsync(
            $"{TasksPath}/{singleTask.Id}/cancel",
            new CancelFarmTaskRequest(singleTask.Version, IncludeRecurrenceGroup: true),
            FarmTaskTestData.JsonOptions,
            ct);
        await nonGroupRes.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.TaskInvalidState, ct);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var states = await db.FarmTasks
            .Where(t => t.RecurrenceGroupId == groupId)
            .ToDictionaryAsync(t => t.Id, t => t.Status, ct);

        Assert.Equal(FarmTaskStatus.Done, states[doneTask.Id]);
        Assert.Equal(FarmTaskStatus.Cancelled, states[pendingTask.Id]);
        Assert.Equal(FarmTaskStatus.Cancelled, states[waitingTask.Id]);
    }

    [Fact(DisplayName = "FR-TSK-09: Lọc danh sách theo mùa vụ, người thực hiện, trạng thái, trễ hạn (IsOverdue), ngày và mine=true")]
    public async Task GetList_FiltersScopeAndComputedOverdue()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var customFactory = new FarmTaskTestData.FixedClockFactory(
            factory.Postgres,
            new DateTimeOffset(2026, 10, 15, 5, 0, 0, TimeSpan.Zero));

        var household = await customFactory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(customFactory.Postgres, household, "TD001", ct);
        var season1 = await TestData.CreateSeasonAsync(customFactory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var season2 = await TestData.CreateSeasonAsync(customFactory.Postgres, household, plot, SeasonStatus.Planned, ct);

        await TestData.AssignMemberAsync(customFactory.Postgres, household, season1, household.Member, ct);
        await TestData.AssignMemberAsync(customFactory.Postgres, household, season1, household.Worker, ct);

        // 1. Trễ hạn (ScheduledDate < 2026-10-15 và Pending) giao cho Worker
        var overdueWorkerTask = await FarmTaskTestData.CreateTaskAsync(
            customFactory.Postgres, household, season1, new DateOnly(2026, 10, 10), ct,
            assignee: household.Worker, status: FarmTaskStatus.Pending);

        // 2. Chờ vật tư (cùng ngày < hôm nay -> cũng tính IsOverdue) giao cho Member
        var overdueWaitingMemberTask = await FarmTaskTestData.CreateTaskAsync(
            customFactory.Postgres, household, season1, new DateOnly(2026, 10, 12), ct,
            assignee: household.Member, status: FarmTaskStatus.WaitingForSupply);

        // 3. Done trong quá khứ -> KHÔNG tính IsOverdue
        await FarmTaskTestData.CreateTaskAsync(
            customFactory.Postgres, household, season1, new DateOnly(2026, 10, 8), ct,
            assignee: household.Worker, status: FarmTaskStatus.Done);

        // 4. Thuộc season2 (Member không được phân công vào season2)
        await FarmTaskTestData.CreateTaskAsync(
            customFactory.Postgres, household, season2, new DateOnly(2026, 10, 20), ct,
            assignee: household.Owner, status: FarmTaskStatus.Pending);

        // Chủ hộ lọc isOverdue=true -> 2 công việc trễ hạn
        var ownerClient = customFactory.CreateClientFor(household.Owner);
        var overdueRes = await ownerClient.GetAsync($"{TasksPath}?isOverdue=true", ct);
        Assert.Equal(HttpStatusCode.OK, overdueRes.StatusCode);
        var overdueItems = (await overdueRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, overdueItems.Count);
        Assert.All(overdueItems, item => Assert.True(item.GetProperty("isOverdue").GetBoolean()));

        // Thành viên hộ lọc không cần seasonId -> chỉ thấy 3 việc của season1, không thấy việc của season2
        var memberClient = customFactory.CreateClientFor(household.Member);
        var memberRes = await memberClient.GetAsync(TasksPath, ct);
        Assert.Equal(HttpStatusCode.OK, memberRes.StatusCode);
        var memberItems = (await memberRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(3, memberItems.Count);

        // mine=true với Member -> chỉ thấy việc của Member
        var mineRes = await memberClient.GetAsync($"{TasksPath}?mine=true", ct);
        var mineItems = (await mineRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
        Assert.Single(mineItems);
        Assert.Equal(overdueWaitingMemberTask.Id, mineItems[0].GetProperty("id").GetGuid());

        // Nhân công chỉ thấy việc giao cho mình (2 việc: 1 Pending quá hạn, 1 Done)
        var workerClient = customFactory.CreateClientFor(household.Worker);
        var workerRes = await workerClient.GetAsync($"{TasksPath}?status=Pending", ct);
        var workerItems = (await workerRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
        Assert.Single(workerItems);
        Assert.Equal(overdueWorkerTask.Id, workerItems[0].GetProperty("id").GetGuid());

        // Assignees API cho season1 (Owner + Member + Worker = 3)
        var assigneesRes = await ownerClient.GetAsync($"{TasksPath}/assignees?seasonId={season1.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, assigneesRes.StatusCode);
        var assignees = (await assigneesRes.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(3, assignees.Count);
    }
}
