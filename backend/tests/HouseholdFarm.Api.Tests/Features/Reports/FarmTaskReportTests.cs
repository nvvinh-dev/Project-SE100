using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Features.Reports;

/// <summary>
/// Báo cáo công việc: thống kê theo thời gian hoặc người phụ trách, không xếp hạng cá nhân,
/// phạm vi phân công và xuất tệp (FR-RPT-TASK-01, FR-RPT-TASK-02, BR-RPT-02, BR-RPT-08).
/// </summary>
public class FarmTaskReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string FarmTasksPath = "/api/reports/farm-tasks";

    [Fact(DisplayName = "FR-RPT-TASK-01, FR-RPT-TASK-02, BR-RPT-08: nhóm theo thời gian, nhóm theo người sắp theo tên, có Chưa giao người")]
    public async Task Get_GroupsByTimeAndAssigneeWithoutRanking()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, season, household.Member, ct);

        var today = factory.Services.GetRequiredService<IAppClock>().Today;
        var pastDay = today.AddDays(-5);
        var futureDay = today.AddDays(5);

        // Tạo 2 thành viên tên B và A để kiểm chứng sắp xếp theo thứ tự chữ cái (A trước B), dù B hoàn thành nhiều hơn A.
        var memberAn = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = household.Household.Id,
            Role = UserRole.Member,
            FullName = "Nguyễn Văn An",
            PhoneNumber = $"09{Random.Shared.Next(10000000, 99999999)}",
            UserName = $"an-{Guid.CreateVersion7():N}",
        };
        var memberBinh = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = household.Household.Id,
            Role = UserRole.Member,
            FullName = "Trần Văn Bình",
            PhoneNumber = $"09{Random.Shared.Next(10000000, 99999999)}",
            UserName = $"binh-{Guid.CreateVersion7():N}",
        };

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.Users.AddRange(memberAn, memberBinh);
            db.FarmTasks.AddRange(
                // An: 1 việc trễ hạn (Pending trong quá khứ) -> hoàn thành 0/1.
                Task(season.Id, memberAn.Id, FarmTaskStatus.Pending, pastDay),
                // Bình: 2 việc Done -> hoàn thành 2/2.
                Task(season.Id, memberBinh.Id, FarmTaskStatus.Done, pastDay),
                Task(season.Id, memberBinh.Id, FarmTaskStatus.Done, futureDay),
                // Chưa giao người: 1 việc WaitingForSupply trong tương lai (chưa trễ hạn).
                Task(season.Id, null, FarmTaskStatus.WaitingForSupply, futureDay),
                // 1 việc Cancelled -> tách khỏi tổng đã lập.
                Task(season.Id, memberAn.Id, FarmTaskStatus.Cancelled, pastDay));
            await db.SaveChangesAsync(ct);
        }

        // 1. Nhóm theo người phụ trách.
        var byAssignee = await GetReportAsync(household.Owner, "groupBy=Assignee", ct);
        var overview = byAssignee.GetProperty("overview");
        Assert.Equal(4, overview.GetProperty("plannedCount").GetInt32());
        Assert.Equal(2, overview.GetProperty("completedCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("overdueCount").GetInt32());
        Assert.Equal(2, overview.GetProperty("pendingCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("cancelledCount").GetInt32());

        var rows = byAssignee.GetProperty("rows").EnumerateArray().ToList();
        Assert.Equal(3, rows.Count);
        // Sắp theo tên ("Nguyễn Văn An" đứng trước "Trần Văn Bình"), "Chưa giao người" đứng cuối.
        Assert.Equal("Nguyễn Văn An", rows[0].GetProperty("label").GetString());
        Assert.Equal("Trần Văn Bình", rows[1].GetProperty("label").GetString());
        Assert.Equal("Chưa giao người", rows[2].GetProperty("label").GetString());
        Assert.True(rows[2].GetProperty("isUnassigned").GetBoolean());

        // BR-RPT-08: không có trường rank/score/completionRate trong payload.
        foreach (var prop in rows[0].EnumerateObject())
        {
            Assert.DoesNotContain("rank", prop.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("score", prop.Name, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("rate", prop.Name, StringComparison.OrdinalIgnoreCase);
        }

        // 2. Nhóm theo thời gian (tháng của ScheduledDate) với khoảng ngày cụ thể.
        var byTime = await GetReportAsync(
            household.Owner,
            $"groupBy=Time&from={pastDay:yyyy-MM-dd}&to={pastDay:yyyy-MM-dd}",
            ct);
        var timeOverview = byTime.GetProperty("overview");
        Assert.Equal(2, timeOverview.GetProperty("plannedCount").GetInt32());
        Assert.Equal(1, timeOverview.GetProperty("completedCount").GetInt32());
        Assert.Equal(1, timeOverview.GetProperty("overdueCount").GetInt32());
        Assert.Equal(1, timeOverview.GetProperty("pendingCount").GetInt32());
        var timeRow = Assert.Single(byTime.GetProperty("rows").EnumerateArray());
        Assert.Equal($"{pastDay.Month:D2}/{pastDay.Year:D4}", timeRow.GetProperty("label").GetString());
    }

    [Fact(DisplayName = "FR-RPT-02: Thành viên chỉ thấy việc trong mùa vụ được phân công; Nhân công bị FORBIDDEN")]
    public async Task Get_EnforcesMemberScopeAndBlocksWorker()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var assignedSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        await TestData.AssignMemberAsync(factory.Postgres, household, assignedSeason, household.Member, ct);
        var otherSeason = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);

        var today = factory.Services.GetRequiredService<IAppClock>().Today;
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.FarmTasks.AddRange(
                Task(assignedSeason.Id, household.Member.Id, FarmTaskStatus.Done, today),
                Task(otherSeason.Id, household.Owner.Id, FarmTaskStatus.Pending, today.AddDays(-2)));
            await db.SaveChangesAsync(ct);
        }

        var memberReport = await GetReportAsync(household.Member, "", ct);
        var overview = memberReport.GetProperty("overview");
        Assert.Equal(1, overview.GetProperty("plannedCount").GetInt32());
        Assert.Equal(1, overview.GetProperty("completedCount").GetInt32());
        Assert.Equal(0, overview.GetProperty("overdueCount").GetInt32());

        var workerResp = await factory.CreateClientFor(household.Worker).GetAsync(FarmTasksPath, ct);
        await workerResp.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-RPT-04, FR-RPT-05, FR-RPT-06: xuất Excel và PDF báo cáo công việc")]
    public async Task Export_ExcelAndPdf_PersistsExportAndAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var today = factory.Services.GetRequiredService<IAppClock>().Today;

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.FarmTasks.Add(Task(season.Id, null, FarmTaskStatus.Pending, today));
            await db.SaveChangesAsync(ct);
        }

        var client = factory.CreateClientFor(household.Owner);
        var excelResp = await client.PostAsJsonAsync($"{FarmTasksPath}/export", new { format = "Excel", groupBy = "Assignee" }, ct);
        Assert.Equal(HttpStatusCode.OK, excelResp.StatusCode);

        var pdfResp = await client.PostAsJsonAsync($"{FarmTasksPath}/export", new { format = "Pdf", groupBy = "Assignee" }, ct);
        Assert.Equal(HttpStatusCode.OK, pdfResp.StatusCode);
        Assert.Equal("application/pdf", pdfResp.Content.Headers.ContentType?.MediaType);

        await using var verifyDb = factory.Postgres.CreateDbContext();
        var exports = await verifyDb.ReportExports.IgnoreQueryFilters()
            .Where(r => r.HouseholdId == household.Household.Id)
            .ToListAsync(ct);
        Assert.Equal(2, exports.Count);
        Assert.All(exports, e => Assert.Equal(ReportType.FarmTask, e.ReportType));
        var auditCount = await verifyDb.AuditLogs.IgnoreQueryFilters()
            .CountAsync(a => a.HouseholdId == household.Household.Id && a.Action == AuditActions.ReportExport, ct);
        Assert.Equal(2, auditCount);
    }

    private async Task<JsonElement> GetReportAsync(ApplicationUser user, string query, CancellationToken ct)
    {
        var path = string.IsNullOrEmpty(query) ? FarmTasksPath : $"{FarmTasksPath}?{query}";
        var response = await factory.CreateClientFor(user).GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data");
    }

    private static FarmTask Task(Guid seasonId, Guid? assigneeId, FarmTaskStatus status, DateOnly scheduledDate) =>
        new()
        {
            SeasonId = seasonId,
            AssigneeId = assigneeId,
            Type = FarmTaskType.Watering,
            ScheduledDate = scheduledDate,
            Status = status,
            CompletedAt = status == FarmTaskStatus.Done ? DateTime.UtcNow : null,
            CancelledAt = status == FarmTaskStatus.Cancelled ? DateTime.UtcNow : null,
        };
}
