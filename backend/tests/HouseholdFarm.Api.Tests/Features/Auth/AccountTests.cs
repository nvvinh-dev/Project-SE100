using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Auth;

/// <summary>Đổi mật khẩu (FR-AUTH-05), sửa thông tin cá nhân (FR-AUTH-07).</summary>
public class AccountTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string NewPassword = "MatKhauMoi2026";

    [Fact(DisplayName = "FR-AUTH-05: đổi mật khẩu thu hồi mọi refresh token, xóa cookie; đăng nhập bằng mật khẩu mới được")]
    public async Task ChangePassword_WithCorrectCurrentPassword_RevokesAllSessions()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = household.Member.PhoneNumber!;
        var first = await AuthApi.LoginAsync(factory, phone, TestData.Password, ct);
        var second = await AuthApi.LoginAsync(factory, phone, TestData.Password, ct);

        var response = await factory.CreateClientFor(household.Member).PostAsJsonAsync(
            AuthApi.ChangePasswordPath,
            new { currentPassword = TestData.Password, newPassword = NewPassword },
            ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(string.Empty, AuthApi.ReadRefreshCookie(response)!.Value.Value);
        await using (var db = factory.Postgres.CreateDbContext())
        {
            Assert.All(
                await db.RefreshTokens.Where(t => t.UserId == household.Member.Id).ToListAsync(ct),
                t => Assert.NotNull(t.RevokedAt));
        }

        foreach (var session in new[] { first, second })
        {
            await (await AuthApi.RefreshAsync(factory, session.RefreshToken, ct))
                .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        }

        await AuthApi.LoginAsync(factory, phone, NewPassword, ct);
        await (await AuthApi.CreateClient(factory).PostAsJsonAsync(
                AuthApi.LoginPath, new { phone, password = TestData.Password }, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
    }

    [Fact(DisplayName = "BR-USR-17: sai mật khẩu hiện tại trả INVALID_CREDENTIALS, không tăng bộ đếm khóa tạm")]
    public async Task ChangePassword_WithWrongCurrentPassword_ReturnsInvalidCredentials()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            AuthApi.ChangePasswordPath,
            new { currentPassword = "SaiMatKhau1", newPassword = NewPassword },
            ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.Id == household.Owner.Id, ct)).AccessFailedCount);
        await AuthApi.LoginAsync(factory, household.Owner.PhoneNumber!, TestData.Password, ct);
    }

    [Fact(DisplayName = "D-22: mật khẩu mới yếu trả VALIDATION_ERROR ở ô mật khẩu mới")]
    public async Task ChangePassword_WithWeakNewPassword_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PostAsJsonAsync(
            AuthApi.ChangePasswordPath,
            new { currentPassword = TestData.Password, newPassword = "khongcoso" },
            ct);

        var problem = await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        Assert.Equal(
            "Mật khẩu phải có ít nhất 1 chữ số.",
            problem.GetProperty("errors").GetProperty("newPassword")[0].GetString());
    }

    [Fact(DisplayName = "FR-AUTH-05: Quản trị hệ thống cũng đổi được mật khẩu của mình")]
    public async Task ChangePassword_SystemAdmin_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var admin = await factory.CreateSystemAdminAsync(ct);

        var response = await factory.CreateClientFor(admin).PostAsJsonAsync(
            AuthApi.ChangePasswordPath,
            new { currentPassword = TestData.Password, newPassword = NewPassword },
            ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact(DisplayName = "FR-AUTH-07: sửa họ tên, số điện thoại rồi đăng nhập bằng số mới; số cũ không đăng nhập được nữa")]
    public async Task UpdateProfile_WithNewPhone_LogsInWithNewPhone()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var oldPhone = household.Worker.PhoneNumber!;
        var newPhone = TestData.NewPhone();

        var response = await factory.CreateClientFor(household.Worker).PutAsJsonAsync(
            AuthApi.MePath,
            new { fullName = " Trần Thị Bình ", phone = $"+84 {newPhone[1..4]} {newPhone[4..7]} {newPhone[7..]}" },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        Assert.Equal("Trần Thị Bình", data.GetProperty("fullName").GetString());
        Assert.Equal(newPhone, data.GetProperty("phone").GetString());
        Assert.Equal("Worker", data.GetProperty("role").GetString());

        await using (var db = factory.Postgres.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == household.Worker.Id, ct);
            Assert.Equal(newPhone, user.UserName);
            Assert.Equal(newPhone, user.NormalizedUserName);
            Assert.Equal(newPhone, user.PhoneNumber);
        }

        await AuthApi.LoginAsync(factory, newPhone, TestData.Password, ct);
        await (await AuthApi.CreateClient(factory).PostAsJsonAsync(
                AuthApi.LoginPath, new { phone = oldPhone, password = TestData.Password }, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
    }

    [Fact(DisplayName = "FR-AUTH-07: giữ nguyên số của mình, chỉ sửa họ tên thì không bị coi là trùng")]
    public async Task UpdateProfile_KeepOwnPhone_Succeeds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner).PutAsJsonAsync(
            AuthApi.MePath,
            new { fullName = "Tên mới", phone = household.Owner.PhoneNumber },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Tên mới", (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("fullName").GetString());
    }

    [Fact(DisplayName = "BR-USR-02: đổi sang số của tài khoản khác trả PHONE_ALREADY_USED")]
    public async Task UpdateProfile_PhoneOfOtherUser_ReturnsPhoneAlreadyUsed()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var otherHousehold = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Member).PutAsJsonAsync(
            AuthApi.MePath,
            new { fullName = "Thành viên", phone = otherHousehold.Owner.PhoneNumber },
            ct);

        await response.AssertProblemAsync(HttpStatusCode.Conflict, ErrorCodes.PhoneAlreadyUsed, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(
            household.Member.PhoneNumber,
            (await db.Users.SingleAsync(u => u.Id == household.Member.Id, ct)).PhoneNumber);
    }
}
