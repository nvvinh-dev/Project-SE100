using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Features.Auth;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Features.Admin;

/// <summary>
/// Quản trị xem danh sách tài khoản (FR-ADM-01), khóa, mở khóa (FR-ADM-02), đặt lại mật khẩu cho Chủ hộ (FR-ADM-03).
/// </summary>
/// <remarks>Database dùng chung cho cả lượt chạy nên danh sách có tài khoản của mọi test: test thu hẹp bằng mã hộ.</remarks>
public class AdminTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string AccountsPath = "/api/admin/accounts";

    [Fact(DisplayName = "FR-ADM-01: Quản trị thấy tài khoản của nhiều hộ kèm mã, tên hộ, vai trò, trạng thái; không có tài khoản Quản trị")]
    public async Task List_Admin_SeesAccountsOfManyHouseholds()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var client = factory.CreateClientFor(admin);

        var a = await ItemsAsync(client, $"?search={householdA.Household.Code}&pageSize=100", ct);
        var b = await ItemsAsync(client, $"?search={householdB.Household.Code}&pageSize=100", ct);

        Assert.Equal(4, a.Count);
        Assert.Equal(4, b.Count);
        var owner = a.Single(i => i.GetProperty("role").GetString() == "Owner");
        Assert.Equal(householdA.Owner.Id, owner.GetProperty("id").GetGuid());
        Assert.Equal(householdA.Owner.PhoneNumber, owner.GetProperty("phone").GetString());
        Assert.Equal(householdA.Household.Code, owner.GetProperty("householdCode").GetString());
        Assert.Equal(householdA.Household.Name, owner.GetProperty("householdName").GetString());
        Assert.False(owner.GetProperty("isLocked").GetBoolean());

        var all = await ItemsAsync(client, "?pageSize=100", ct);
        Assert.DoesNotContain(all, i => i.GetProperty("id").GetGuid() == admin.Id);
        Assert.DoesNotContain(all, i => i.GetProperty("role").GetString() == "SystemAdmin");
    }

    [Fact(DisplayName = "FR-ADM-01: tìm theo họ tên, số điện thoại; lọc vai trò, trạng thái khóa")]
    public async Task List_SearchesAndFilters()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var client = factory.CreateClientFor(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{AccountsPath}/{household.Worker.Id}/lock", null, ct)).StatusCode);
        var code = household.Household.Code;

        Assert.Equal([household.Member.Id], Ids(await ItemsAsync(client, $"?search={household.Member.PhoneNumber}", ct)));
        Assert.Equal([household.Owner.Id], Ids(await ItemsAsync(client, $"?search={code}&role=Owner", ct)));
        Assert.Equal([household.Worker.Id], Ids(await ItemsAsync(client, $"?search={code}&isLocked=true", ct)));
        Assert.Equal(3, (await ItemsAsync(client, $"?search={code}&isLocked=false", ct)).Count);
        await (await client.GetAsync($"{AccountsPath}?role=SystemAdmin", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-ADM-01: xem chi tiết một tài khoản; tài khoản Quản trị, id không có thật trả NOT_FOUND")]
    public async Task Get_Account()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var otherAdmin = await factory.CreateSystemAdminAsync(ct);
        var client = factory.CreateClientFor(admin);

        var response = await client.GetAsync($"{AccountsPath}/{household.Worker.Id}", ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Worker", data.GetProperty("role").GetString());
        Assert.Equal(household.Household.Code, data.GetProperty("householdCode").GetString());
        foreach (var id in new[] { otherAdmin.Id, admin.Id, Guid.CreateVersion7() })
        {
            await (await client.GetAsync($"{AccountsPath}/{id}", ct))
                .AssertProblemAsync(HttpStatusCode.NotFound, ErrorCodes.NotFound, ct);
        }
    }

    [Fact(DisplayName = "Ma trận 2.5: Chủ hộ, Thành viên hộ, Nhân công gọi API quản trị trả FORBIDDEN")]
    public async Task NotAdmin_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        foreach (var user in new[] { household.Owner, household.MemberWithAllPermissions, household.Worker })
        {
            var client = factory.CreateClientFor(user);
            await (await client.GetAsync(AccountsPath, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
            await (await client.PostAsync($"{AccountsPath}/{household.Member.Id}/lock", null, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "FR-ADM-02: khóa Chủ hộ thì phiên cũ hết, đăng nhập bị ACCOUNT_LOCKED; nhật ký vào hộ của Chủ hộ, Chủ hộ xem được")]
    public async Task Lock_Owner_EndsSessionsAndLogsToHousehold()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var phone = household.Owner.PhoneNumber!;
        var session = await AuthApi.LoginAsync(factory, phone, TestData.Password, ct);

        var response = await factory.CreateClientFor(admin).PostAsync($"{AccountsPath}/{household.Owner.Id}/lock", null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("isLocked").GetBoolean());
        await (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        await (await AuthApi.CreateClient(factory).PostAsJsonAsync(AuthApi.LoginPath, new { phone, password = TestData.Password }, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.AccountLocked, ct);
        await (await factory.CreateClientFor(household.Owner).GetAsync("/api/plots", ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.AccountLocked, ct);

        var log = await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres,
            household.Household.Id,
            AuditActions.ApplicationUserLock,
            household.Owner.Id,
            ct);
        Assert.Equal(admin.Id, log.ActorId);
    }

    [Fact(DisplayName = "FR-ADM-02: mở khóa thì đăng nhập lại được; Chủ hộ thấy hai dòng khóa, mở khóa của Quản trị trong nhật ký hộ")]
    public async Task Unlock_Member_CanLogInAndOwnerSeesLogs()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var client = factory.CreateClientFor(admin);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsync($"{AccountsPath}/{household.Member.Id}/lock", null, ct)).StatusCode);

        var response = await client.PostAsync($"{AccountsPath}/{household.Member.Id}/unlock", null, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False((await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("isLocked").GetBoolean());
        await AuthApi.LoginAsync(factory, household.Member.PhoneNumber!, TestData.Password, ct);

        var logs = (await (await factory.CreateClientFor(household.Owner).GetAsync("/api/audit-logs", ct)).ReadJsonAsync(ct))
            .GetProperty("data").EnumerateArray().ToList();
        Assert.Equal(
            [AuditActions.ApplicationUserUnlock, AuditActions.ApplicationUserLock],
            logs.Select(l => l.GetProperty("action").GetString()));
        Assert.All(logs, l => Assert.Equal(admin.FullName, l.GetProperty("actorName").GetString()));
    }

    [Fact(DisplayName = "FR-ADM-03: đặt lại mật khẩu Chủ hộ thì mật khẩu mới dùng được, mật khẩu cũ và phiên cũ hết hiệu lực")]
    public async Task ResetPassword_Owner_NewPasswordWorks()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var phone = household.Owner.PhoneNumber!;
        var session = await AuthApi.LoginAsync(factory, phone, TestData.Password, ct);

        var response = await factory.CreateClientFor(admin).PostAsJsonAsync(
            $"{AccountsPath}/{household.Owner.Id}/reset-password",
            new { newPassword = "ChuHoMoi2026" },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        await (await AuthApi.CreateClient(factory).PostAsJsonAsync(AuthApi.LoginPath, new { phone, password = TestData.Password }, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        await AuthApi.LoginAsync(factory, phone, "ChuHoMoi2026", ct);
    }

    [Fact(DisplayName = "FR-ADM-03: đặt lại mật khẩu cho Thành viên hộ, Nhân công trả FORBIDDEN; mật khẩu yếu trả VALIDATION_ERROR")]
    public async Task ResetPassword_NotOwnerOrWeak_IsRejected()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var client = factory.CreateClientFor(admin);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            await (await client.PostAsJsonAsync($"{AccountsPath}/{user.Id}/reset-password", new { newPassword = "MatKhauMoi2026" }, ct))
                .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }

        var weak = await client.PostAsJsonAsync($"{AccountsPath}/{household.Owner.Id}/reset-password", new { newPassword = "12345678" }, ct);
        var problem = await weak.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal("Mật khẩu phải có ít nhất 1 chữ cái.", problem.GetProperty("errors").GetProperty("newPassword")[0].GetString());
        await AuthApi.LoginAsync(factory, household.Member.PhoneNumber!, TestData.Password, ct);
    }

    [Fact(DisplayName = "BR-USR-15: Chủ hộ đang bị khóa tạm vì nhập sai nhiều lần, được đặt lại mật khẩu thì đăng nhập được ngay")]
    public async Task ResetPassword_TemporarilyLockedOwner_ClearsLockout()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var phone = household.Owner.PhoneNumber!;
        await TestData.SetTemporaryLockoutAsync(factory.Postgres, household.Owner.Id, ct);
        await (await AuthApi.CreateClient(factory).PostAsJsonAsync(AuthApi.LoginPath, new { phone, password = TestData.Password }, ct))
            .AssertProblemAsync(HttpStatusCode.TooManyRequests, ErrorCodes.TooManyAttempts, ct);

        var response = await factory.CreateClientFor(admin).PostAsJsonAsync(
            $"{AccountsPath}/{household.Owner.Id}/reset-password",
            new { newPassword = "ChuHoMoi2026" },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await AuthApi.LoginAsync(factory, phone, "ChuHoMoi2026", ct);
    }

    private async Task<List<System.Text.Json.JsonElement>> ItemsAsync(HttpClient client, string queryString, CancellationToken ct)
    {
        var response = await client.GetAsync(AccountsPath + queryString, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data").EnumerateArray().ToList();
    }

    private static List<Guid> Ids(IEnumerable<System.Text.Json.JsonElement> items) =>
        items.Select(i => i.GetProperty("id").GetGuid()).ToList();
}
