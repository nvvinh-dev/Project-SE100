using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Features.Auth;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Households;

/// <summary>
/// Đổi vai trò (FR-HH-04), khóa (FR-HH-05), mở khóa (FR-HH-06), quyền bổ sung (FR-HH-07), đặt lại mật khẩu (FR-HH-09).
/// </summary>
public class MemberAccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string MembersPath = "/api/members";

    [Fact(DisplayName = "BR-USR-04, BR-USR-09: đổi Thành viên hộ có quyền sang Nhân công tắt ba quyền, thu hồi phiên, ghi nhật ký")]
    public async Task ChangeRole_MemberToWorker_RemovesPermissionsRevokesSessionsAndLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var target = household.MemberWithAllPermissions;
        var session = await AuthApi.LoginAsync(factory, target.PhoneNumber!, TestData.Password, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            $"{MembersPath}/{target.Id}/change-role",
            new { role = "Worker", version = target.Version },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Worker", data.GetProperty("role").GetString());
        Assert.False(data.GetProperty("canManageStock").GetBoolean());
        Assert.False(data.GetProperty("canManageFinance").GetBoolean());
        Assert.False(data.GetProperty("canCloseSeason").GetBoolean());

        await (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == target.Id, ct);
        Assert.Equal(AuditActions.ApplicationUserChangeRole, log.Action);
        var oldValues = JsonDocument.Parse(log.OldValues!).RootElement;
        var newValues = JsonDocument.Parse(log.NewValues!).RootElement;
        Assert.Equal(
            ["canCloseSeason", "canManageFinance", "canManageStock", "role"],
            oldValues.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("Member", oldValues.GetProperty("role").GetString());
        Assert.Equal("Worker", newValues.GetProperty("role").GetString());
        Assert.False(newValues.GetProperty("canManageFinance").GetBoolean());
    }

    [Fact(DisplayName = "BR-USR-07, BR-USR-08: đổi vai trò của Chủ hộ trả FORBIDDEN; đổi sang Chủ hộ trả VALIDATION_ERROR")]
    public async Task ChangeRole_OwnerOrToOwner_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);

        await (await client.PostAsJsonAsync(
                $"{MembersPath}/{household.Owner.Id}/change-role",
                new { role = "Member", version = household.Owner.Version },
                ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await (await client.PostAsJsonAsync(
                $"{MembersPath}/{household.Member.Id}/change-role",
                new { role = "Owner", version = household.Member.Version },
                ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-HH-04: đổi Nhân công sang Thành viên hộ không có lại quyền nào; giữ nguyên vai trò thì không thu hồi phiên")]
    public async Task ChangeRole_WorkerToMemberAndSameRole()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        var session = await AuthApi.LoginAsync(factory, household.Member.PhoneNumber!, TestData.Password, ct);

        var promoted = await client.PostAsJsonAsync(
            $"{MembersPath}/{household.Worker.Id}/change-role",
            new { role = "Member", version = household.Worker.Version },
            ct);
        var unchanged = await client.PostAsJsonAsync(
            $"{MembersPath}/{household.Member.Id}/change-role",
            new { role = "Member", version = household.Member.Version },
            ct);

        var data = (await promoted.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Member", data.GetProperty("role").GetString());
        Assert.False(data.GetProperty("canManageStock").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, unchanged.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct)).StatusCode);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.AuditLogs.IgnoreQueryFilters().AnyAsync(l => l.EntityId == household.Member.Id, ct));
    }

    [Fact(DisplayName = "FR-HH-07, D-11: bật Quản lý tài chính thì thành viên gọi được API chi phí ngay; nhật ký chỉ có quyền bị đổi")]
    public async Task ChangePermissions_EnableFinance_TakesEffectImmediately()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var memberClient = factory.CreateClientFor(household.Member);
        await (await memberClient.GetAsync("/api/expenses", ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            $"{MembersPath}/{household.Member.Id}/change-permissions",
            new { canManageStock = false, canManageFinance = true, canCloseSeason = false, version = household.Member.Version },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("canManageFinance").GetBoolean());
        Assert.Equal(HttpStatusCode.OK, (await memberClient.GetAsync("/api/expenses", ct)).StatusCode);

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == household.Member.Id, ct);
        Assert.Equal(AuditActions.ApplicationUserChangePermissions, log.Action);
        Assert.Equal(["canManageFinance"], JsonDocument.Parse(log.NewValues!).RootElement.EnumerateObject().Select(p => p.Name));
    }

    [Fact(DisplayName = "BR-USR-09: bật quyền bổ sung cho Nhân công trả VALIDATION_ERROR, không lưu")]
    public async Task ChangePermissions_Worker_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            $"{MembersPath}/{household.Worker.Id}/change-permissions",
            new { canManageStock = true, canManageFinance = false, canCloseSeason = false, version = household.Worker.Version },
            ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False((await db.Users.SingleAsync(u => u.Id == household.Worker.Id, ct)).CanManageStock);
    }

    [Fact(DisplayName = "TC-23: Chủ hộ khóa thành viên đang đăng nhập; thành viên làm mới phiên thất bại, gọi API bị ACCOUNT_LOCKED")]
    public async Task TC23_Lock_MemberLoggedIn_RefreshFails()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var session = await AuthApi.LoginAsync(factory, household.Member.PhoneNumber!, TestData.Password, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsync($"{MembersPath}/{household.Member.Id}/lock", null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("isLocked").GetBoolean());
        await (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        await (await factory.CreateClientFor(household.Member).GetAsync("/api/plots", ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.AccountLocked, ct);

        await using var db = factory.Postgres.CreateDbContext();
        var log = await db.AuditLogs.IgnoreQueryFilters().SingleAsync(l => l.EntityId == household.Member.Id, ct);
        Assert.Equal(AuditActions.ApplicationUserLock, log.Action);
        Assert.Equal(household.Owner.Id, log.ActorId);
    }

    [Fact(DisplayName = "FR-HH-06: mở khóa thì thành viên đăng nhập lại bằng mật khẩu cũ được; có nhật ký mở khóa")]
    public async Task Unlock_LockedMember_CanLogInAgain()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Owner);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{MembersPath}/{household.Worker.Id}/lock", null, ct)).StatusCode);

        var response = await client.PostAsync($"{MembersPath}/{household.Worker.Id}/unlock", null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("isLocked").GetBoolean());
        await AuthApi.LoginAsync(factory, household.Worker.PhoneNumber!, TestData.Password, ct);
        await using var db = factory.Postgres.CreateDbContext();
        var actions = await db.AuditLogs.IgnoreQueryFilters()
            .Where(l => l.EntityId == household.Worker.Id)
            .OrderBy(l => l.OccurredAt)
            .Select(l => l.Action)
            .ToListAsync(ct);
        Assert.Equal([AuditActions.ApplicationUserLock, AuditActions.ApplicationUserUnlock], actions);
    }

    [Fact(DisplayName = "BR-USR-07: Chủ hộ khóa, đặt lại mật khẩu tài khoản Chủ hộ trả FORBIDDEN; Thành viên hộ khóa người khác trả FORBIDDEN")]
    public async Task LockOrResetOwner_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var owner = factory.CreateClientFor(household.Owner);

        await (await owner.PostAsync($"{MembersPath}/{household.Owner.Id}/lock", null, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await (await owner.PostAsJsonAsync(
                $"{MembersPath}/{household.Owner.Id}/reset-password",
                new { newPassword = "MatKhauMoi2026" },
                ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await (await factory.CreateClientFor(household.MemberWithAllPermissions)
                .PostAsync($"{MembersPath}/{household.Worker.Id}/lock", null, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);

        await using var db = factory.Postgres.CreateDbContext();
        Assert.False((await db.Users.SingleAsync(u => u.Id == household.Worker.Id, ct)).IsLocked);
    }

    [Fact(DisplayName = "FR-HH-09: đặt lại mật khẩu thì đăng nhập bằng mật khẩu mới được, mật khẩu cũ và refresh token cũ hết hiệu lực")]
    public async Task ResetPassword_Member_NewPasswordWorksAndSessionsEnd()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = household.Member.PhoneNumber!;
        var session = await AuthApi.LoginAsync(factory, phone, TestData.Password, ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            $"{MembersPath}/{household.Member.Id}/reset-password",
            new { newPassword = "MatKhauMoi2026" },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        await (await AuthApi.CreateClient(factory).PostAsJsonAsync(
                AuthApi.LoginPath,
                new { phone, password = TestData.Password },
                ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        await AuthApi.LoginAsync(factory, phone, "MatKhauMoi2026", ct);
    }

    [Fact(DisplayName = "D-22: đặt lại mật khẩu yếu trả VALIDATION_ERROR")]
    public async Task ResetPassword_WeakPassword_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            $"{MembersPath}/{household.Member.Id}/reset-password",
            new { newPassword = "1234" },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.True(problem.GetProperty("errors").TryGetProperty("newPassword", out _));
    }
}
