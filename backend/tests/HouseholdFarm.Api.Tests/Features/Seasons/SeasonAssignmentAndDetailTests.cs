using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Seasons;

public class SeasonAssignmentAndDetailTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SeasonsPath = "/api/seasons";
    private const string AssignmentsPath = "/api/season-assignments";
    private const string NotesPath = "/api/season-notes";

    [Fact(DisplayName = "FR-SEA-08: Chủ hộ bắt đầu mùa vụ chuyển từ Planned sang InProgress")]
    public async Task Start_WhenPlanned_ReturnsInProgress()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Owner);
        var response = await client.PostAsync($"{SeasonsPath}/{season.Id}/start", null, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("InProgress", data.GetProperty("status").GetString());

        // Kiểm tra database
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var updated = await db.Seasons.SingleAsync(s => s.Id == season.Id, ct);
        Assert.Equal(SeasonStatus.InProgress, updated.Status);
        Assert.NotNull(updated.StartedAt);
    }

    [Fact(DisplayName = "BR-SEA-10: Bắt đầu mùa vụ không ở trạng thái Planned trả SEASON_INVALID_STATE")]
    public async Task Start_WhenNotPlanned_ReturnsSeasonInvalidState()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        var client = factory.CreateClientFor(household.Owner);
        var response = await client.PostAsync($"{SeasonsPath}/{season.Id}/start", null, ct);
        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonInvalidState, ct);
    }

    [Fact(DisplayName = "BR-SEA-03, TC-02: Bắt đầu mùa vụ khi thửa đang có mùa vụ InProgress khác trả SEASON_PLOT_BUSY")]
    public async Task Start_WhenPlotBusy_ReturnsSeasonPlotBusy()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var season2 = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Owner);
        var response = await client.PostAsync($"{SeasonsPath}/{season2.Id}/start", null, ct);
        await response.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.SeasonPlotBusy, ct);
    }

    [Fact(DisplayName = "FR-SEA-12: Xem chi tiết mùa vụ bởi Chủ hộ trả đầy đủ thông tin")]
    public async Task GetDetail_ByOwner_ReturnsFullDetails()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Owner);
        var response = await client.GetAsync($"{SeasonsPath}/{season.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(season.Id, data.GetProperty("id").GetGuid());
        Assert.False(data.GetProperty("isOutOfScope").GetBoolean());
        Assert.True(data.GetProperty("canStart").GetBoolean());
        Assert.True(data.GetProperty("canEdit").GetBoolean());
    }

    [Fact(DisplayName = "BR-USR-13, BR-GEN-02: Thành viên ngoài phạm vi không có quyền Kho/TC xem chi tiết trả NOT_FOUND")]
    public async Task GetDetail_ByMemberOutOfScope_ReturnsNotFound()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Member);
        var response = await client.GetAsync($"{SeasonsPath}/{season.Id}", ct);
        await response.AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
    }

    [Fact(DisplayName = "FR-SEA-06, BR-USR-10: Chủ hộ phân công Thành viên vào mùa vụ thành công")]
    public async Task Assign_Member_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonAssignmentRequest(season.Id, household.Member.Id);
        var response = await client.PostAsJsonAsync(AssignmentsPath, request, ct);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(season.Id, data.GetProperty("seasonId").GetGuid());
        Assert.Equal(household.Member.Id, data.GetProperty("userId").GetGuid());

        // Đọc lại danh sách phân công
        var listResponse = await client.GetAsync($"{AssignmentsPath}?seasonId={season.Id}", ct);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listData = (await listResponse.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal(1, listData.GetArrayLength());
    }

    [Fact(DisplayName = "BR-USR-10: Phân công Chủ hộ trả VALIDATION_ERROR")]
    public async Task Assign_Owner_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.Planned, ct);

        var client = factory.CreateClientFor(household.Owner);
        var request = new CreateSeasonAssignmentRequest(season.Id, household.Owner.Id);
        var response = await client.PostAsJsonAsync(AssignmentsPath, request, ct);
        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "BR-USR-12, BR-GEN-14, TC-22: Thu hồi phân công khi còn việc chưa làm cần xác nhận")]
    public async Task Unassign_WithPendingTasks_RequiresConfirmation_ThenSucceeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        // Tạo phân công
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var assignment = new SeasonAssignment
        {
            SeasonId = season.Id,
            UserId = household.Member.Id,
        };
        db.SeasonAssignments.Add(assignment);

        // Tạo công việc Pending cho Member này
        var task = new FarmTask
        {
            SeasonId = season.Id,
            AssigneeId = household.Member.Id,
            Type = FarmTaskType.Watering,
            ScheduledDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
            Status = FarmTaskStatus.Pending,
        };
        db.FarmTasks.Add(task);
        await db.SaveChangesAsync(ct);

        var client = factory.CreateClientFor(household.Owner);

        // Lần 1: Chưa confirmed -> trả CONFIRMATION_REQUIRED (422)
        var response1 = await client.PostAsJsonAsync(
            $"{AssignmentsPath}/{assignment.Id}/unassign",
            new UnassignSeasonAssignmentRequest(Confirmed: false),
            ct);
        await response1.AssertProblemAsync(HttpStatusCode.UnprocessableEntity, ErrorCodes.ConfirmationRequired, ct);

        // Lần 2: Confirmed = true -> thành công, task chuyển về AssigneeId = null
        var response2 = await client.PostAsJsonAsync(
            $"{AssignmentsPath}/{assignment.Id}/unassign",
            new UnassignSeasonAssignmentRequest(Confirmed: true),
            ct);
        Assert.Equal(HttpStatusCode.OK, response2.StatusCode);

        // Kiểm tra database: assignment đã bị xóa, task.AssigneeId = null
        await using var dbCheck = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var assignmentExists = await dbCheck.SeasonAssignments.AnyAsync(a => a.Id == assignment.Id, ct);
        Assert.False(assignmentExists);

        var updatedTask = await dbCheck.FarmTasks.SingleAsync(t => t.Id == task.Id, ct);
        Assert.Null(updatedTask.AssigneeId);
    }

    [Fact(DisplayName = "FR-SEA-13, BR-SEA-11: Tạo và cập nhật ghi chú kinh nghiệm")]
    public async Task Notes_CreateAndUpdate_Success()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        var clientOwner = factory.CreateClientFor(household.Owner);

        // Tạo ghi chú
        var createRequest = new CreateSeasonNoteRequest(season.Id, "Kinh nghiệm bón phân đợt 1");
        var createResponse = await clientOwner.PostAsJsonAsync(NotesPath, createRequest, ct);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var noteData = (await createResponse.ReadJsonAsync(ct)).GetProperty("data");
        var noteId = noteData.GetProperty("id").GetGuid();
        var version = noteData.GetProperty("version").GetUInt32();
        Assert.Equal("Kinh nghiệm bón phân đợt 1", noteData.GetProperty("content").GetString());
        Assert.True(noteData.GetProperty("canEdit").GetBoolean());

        // Sửa ghi chú bởi chính người tạo
        var updateRequest = new UpdateSeasonNoteRequest("Kinh nghiệm bón phân đợt 1 - cập nhật liều lượng", version);
        var updateResponse = await clientOwner.PutAsJsonAsync($"{NotesPath}/{noteId}", updateRequest, ct);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        var updatedData = (await updateResponse.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Kinh nghiệm bón phân đợt 1 - cập nhật liều lượng", updatedData.GetProperty("content").GetString());
    }

    [Fact(DisplayName = "BR-SEA-11: Sửa ghi chú của người khác trả FORBIDDEN")]
    public async Task Notes_UpdateByOtherUser_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        // Phân công Member vào mùa vụ
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.SeasonAssignments.Add(new SeasonAssignment { SeasonId = season.Id, UserId = household.Member.Id });
        await db.SaveChangesAsync(ct);

        var clientOwner = factory.CreateClientFor(household.Owner);
        var clientMember = factory.CreateClientFor(household.Member);

        // Owner tạo ghi chú
        var createResponse = await clientOwner.PostAsJsonAsync(
            NotesPath,
            new CreateSeasonNoteRequest(season.Id, "Ghi chú của Chủ hộ"),
            ct);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var noteData = (await createResponse.ReadJsonAsync(ct)).GetProperty("data");
        var noteId = noteData.GetProperty("id").GetGuid();
        var version = noteData.GetProperty("version").GetUInt32();

        // Member cố sửa ghi chú của Owner -> FORBIDDEN (403)
        var updateResponse = await clientMember.PutAsJsonAsync(
            $"{NotesPath}/{noteId}",
            new UpdateSeasonNoteRequest("Member cố sửa", version),
            ct);
        await updateResponse.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }
}
