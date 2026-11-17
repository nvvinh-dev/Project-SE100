using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Features.AuditLogs;

/// <summary>
/// Nhật ký thao tác: đủ hành động (FR-AUD-01), nội dung một dòng (FR-AUD-02), Chủ hộ xem và lọc (FR-AUD-03),
/// không sửa, xóa được (FR-AUD-04).
/// </summary>
public class AuditLogTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AuditLogsPath = "/api/audit-logs";

    // Bảng "Hành động nhật ký" của glossary mục 9.
    private static readonly string[] _glossaryActions =
    [
        "StockTransaction.StockIn", "StockTransaction.StockOut", "StockTransaction.Adjust", "StockTransaction.Void",
        "Expense.Create", "Expense.Correct", "Expense.Void",
        "HarvestRecord.Create", "HarvestRecord.Correct", "HarvestRecord.Void",
        "ProduceStockAdjustment.Adjust", "ProduceStockAdjustment.Void",
        "Sale.Create", "Sale.Correct", "Sale.Void", "Payment.Create", "Payment.Void",
        "Season.Start", "Season.Close", "Season.Cancel", "Season.Reopen",
        "SeasonAssignment.Assign", "SeasonAssignment.Unassign",
        "ApplicationUser.ChangeRole", "ApplicationUser.ChangePermissions", "ApplicationUser.Lock", "ApplicationUser.Unlock",
        "Household.ChangeSettings", "Household.SupportAccess",
        "Report.Export",
    ];

    [Fact(DisplayName = "FR-AUD-01: hằng AuditActions khớp đủ, đúng tên bảng hành động nhật ký của glossary")]
    public void AuditActions_MatchGlossary()
    {
        Assert.Equal(_glossaryActions.Order(), AuditActions.All.Order());
    }

    [Fact(DisplayName = "FR-AUD-02: một dòng nhật ký có người thực hiện, thời điểm theo đồng hồ, đối tượng; chỉ lưu trường bị đổi")]
    public async Task Log_ContainsActorTimeAndOnlyChangedFields()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        // Đồng hồ giả lập đứng yên ở một thời điểm gần hiện tại (để access token còn hạn), làm tròn tới giây.
        var system = TimeProvider.System.GetUtcNow();
        var now = new DateTimeOffset(system.Year, system.Month, system.Day, system.Hour, system.Minute, system.Second, TimeSpan.Zero);
        await using var app = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(now))));
        var client = ClientFor(app, household.Owner);

        var response = await client.PostAsJsonAsync(
            $"/api/members/{household.MemberWithAllPermissions.Id}/change-permissions",
            new { canManageStock = true, canManageFinance = false, canCloseSeason = true, version = household.MemberWithAllPermissions.Version },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var log = await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.ApplicationUserChangePermissions,
            household.MemberWithAllPermissions.Id,
            ct);
        Assert.Equal(household.Owner.Id, log.ActorId);
        Assert.Equal("ApplicationUser", log.EntityType);
        Assert.Equal(now.UtcDateTime, log.OccurredAt);
        Assert.Null(log.Reason);
        Assert.Equal(["canManageFinance"], AuditLogAssertions.FieldNames(log.OldValues));
        Assert.Equal(["canManageFinance"], AuditLogAssertions.FieldNames(log.NewValues));
        Assert.True(JsonDocument.Parse(log.OldValues!).RootElement.GetProperty("canManageFinance").GetBoolean());
        Assert.False(JsonDocument.Parse(log.NewValues!).RootElement.GetProperty("canManageFinance").GetBoolean());
    }

    [Fact(DisplayName = "BR-GEN-07: thao tác bị từ chối thì không có dòng nhật ký nào")]
    public async Task RejectedOperation_WritesNoLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            $"/api/members/{household.Worker.Id}/change-permissions",
            new { canManageStock = true, canManageFinance = false, canCloseSeason = false, version = household.Worker.Version },
            ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await AuditLogAssertions.AssertNoneAsync(factory.Postgres, household.Worker.Id, ct);
    }

    [Fact(DisplayName = "FR-AUD-03: Chủ hộ xem nhật ký của hộ mình, mới nhất trước, kèm họ tên người thực hiện và giá trị cũ, mới")]
    public async Task List_Owner_SeesOwnHouseholdNewestFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var other = await factory.CreateHouseholdAsync(ct);
        await AddLogAsync(household, household.Owner, AuditActions.ApplicationUserLock, household.Worker.Id, At(2026, 11, 1, 8), ct);
        await AddLogAsync(household, household.Owner, AuditActions.ApplicationUserUnlock, household.Worker.Id, At(2026, 11, 2, 8), ct);
        await AddLogAsync(other, other.Owner, AuditActions.ApplicationUserLock, other.Worker.Id, At(2026, 11, 3, 8), ct);

        var body = await (await factory.CreateClientFor(household.Owner).GetAsync(AuditLogsPath, ct)).ReadJsonAsync(ct);

        var items = body.GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(2, body.GetProperty("meta").GetProperty("totalCount").GetInt32());
        Assert.Equal(
            [AuditActions.ApplicationUserUnlock, AuditActions.ApplicationUserLock],
            items.Select(i => i.GetProperty("action").GetString()));
        var newest = items[0];
        Assert.Equal(household.Owner.FullName, newest.GetProperty("actorName").GetString());
        Assert.Equal("ApplicationUser", newest.GetProperty("entityType").GetString());
        Assert.Equal(household.Worker.Id, newest.GetProperty("entityId").GetGuid());
        Assert.True(newest.GetProperty("oldValues").GetProperty("isLocked").GetBoolean());
        Assert.False(newest.GetProperty("newValues").GetProperty("isLocked").GetBoolean());
    }

    [Fact(DisplayName = "FR-AUD-03: lọc theo khoảng ngày giờ Việt Nam (tính cả hai đầu), người thực hiện, loại hành động")]
    public async Task List_FiltersByDateActorAndAction()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        // 31/10 23:30 giờ Việt Nam là 31/10 16:30 UTC; 01/11 00:30 giờ Việt Nam là 31/10 17:30 UTC.
        var lateOct31 = await AddLogAsync(household, household.Owner, AuditActions.HouseholdChangeSettings, household.Household.Id, At(2026, 10, 31, 16, 30), ct);
        var earlyNov1 = await AddLogAsync(household, household.Owner, AuditActions.ApplicationUserLock, household.Worker.Id, At(2026, 10, 31, 17, 30), ct);
        var nov2ByMember = await AddLogAsync(household, household.MemberWithAllPermissions, AuditActions.SeasonStart, Guid.CreateVersion7(), At(2026, 11, 2, 3), ct);
        var client = factory.CreateClientFor(household.Owner);

        Assert.Equal([earlyNov1, nov2ByMember], (await IdsAsync(client, "?from=2026-11-01", ct)).Order());
        Assert.Equal([lateOct31], await IdsAsync(client, "?to=2026-10-31", ct));
        Assert.Equal([earlyNov1], await IdsAsync(client, "?from=2026-11-01&to=2026-11-01", ct));
        Assert.Equal([nov2ByMember], await IdsAsync(client, $"?actorId={household.MemberWithAllPermissions.Id}", ct));
        Assert.Equal([earlyNov1], await IdsAsync(client, "?action=ApplicationUser.Lock", ct));
    }

    [Fact(DisplayName = "Lọc khoảng ngày ngược, loại hành động không có thật trả VALIDATION_ERROR")]
    public async Task List_InvalidFilter_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        var reversed = await (await client.GetAsync($"{AuditLogsPath}?from=2026-11-05&to=2026-11-01", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var unknown = await (await client.GetAsync($"{AuditLogsPath}?action=Season.Delete", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        Assert.True(reversed.GetProperty("errors").TryGetProperty("to", out _));
        Assert.True(unknown.GetProperty("errors").TryGetProperty("action", out _));
    }

    [Fact(DisplayName = "Ma trận 2.5: Thành viên hộ (kể cả đủ quyền), Nhân công, Quản trị xem nhật ký thao tác trả FORBIDDEN")]
    public async Task List_NotOwner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);

        foreach (var user in new[] { household.MemberWithAllPermissions, household.Worker, admin })
        {
            await (await factory.CreateClientFor(user).GetAsync(AuditLogsPath, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "FR-AUD-04: sửa, xóa một dòng nhật ký qua AppDbContext của ứng dụng bị chặn, dữ liệu giữ nguyên")]
    public async Task ModifyOrDelete_ThroughDbContext_IsBlocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var id = await AddLogAsync(household, household.Owner, AuditActions.ApplicationUserLock, household.Worker.Id, At(2026, 11, 1, 8), ct);

        foreach (var delete in new[] { false, true })
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Id == id, ct);
            if (delete)
            {
                db.AuditLogs.Remove(log);
            }
            else
            {
                log.Reason = "Sửa lén";
            }

            await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync(ct));
        }

        await using var check = factory.Postgres.CreateDbContext();
        var saved = await check.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.Id == id, ct);
        Assert.Null(saved.Reason);
    }

    [Fact(DisplayName = "FR-AUD-04: không có API sửa, xóa nhật ký")]
    public async Task UpdateOrDelete_Api_DoesNotExist()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var id = await AddLogAsync(household, household.Owner, AuditActions.ApplicationUserLock, household.Worker.Id, At(2026, 11, 1, 8), ct);
        var client = factory.CreateClientFor(household.Owner);

        var delete = await client.DeleteAsync($"{AuditLogsPath}/{id}", ct);
        var put = await client.PutAsJsonAsync($"{AuditLogsPath}/{id}", new { reason = "Sửa" }, ct);
        var correct = await client.PostAsJsonAsync($"{AuditLogsPath}/{id}/correct", new { reason = "Sửa" }, ct);

        Assert.All([delete, put, correct], r => Assert.False(r.IsSuccessStatusCode));
        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.ApplicationUserLock,
            household.Worker.Id,
            ct);
    }

    private static DateTime At(int year, int month, int day, int hour, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);

    // Dòng nhật ký ghi thẳng database để chọn được thời điểm; nội dung giống dòng của thao tác khóa, mở khóa.
    private async Task<Guid> AddLogAsync(
        TestHousehold household,
        ApplicationUser actor,
        string action,
        Guid entityId,
        DateTime occurredAt,
        CancellationToken ct)
    {
        var locked = action == AuditActions.ApplicationUserLock;
        var log = new AuditLog
        {
            HouseholdId = household.Household.Id,
            ActorId = actor.Id,
            Action = action,
            EntityType = action[..action.IndexOf('.')],
            EntityId = entityId,
            OldValues = JsonSerializer.Serialize(new { isLocked = !locked }, JsonSerializerOptions.Web),
            NewValues = JsonSerializer.Serialize(new { isLocked = locked }, JsonSerializerOptions.Web),
            OccurredAt = occurredAt,
        };
        await using var db = factory.Postgres.CreateDbContext();
        db.AuditLogs.Add(log);
        await db.SaveChangesAsync(ct);
        return log.Id;
    }

    private async Task<List<Guid>> IdsAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync(AuditLogsPath + queryString, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray()
            .Select(l => l.GetProperty("id").GetGuid())
            .ToList();
    }

    // Client của ứng dụng đã thay đồng hồ: access token phải do chính ứng dụng đó cấp.
    private static HttpClient ClientFor(Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> app, ApplicationUser user)
    {
        var client = app.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
            "Bearer",
            app.Services.GetRequiredService<AccessTokenIssuer>().Create(user.Id));
        return client;
    }
}
