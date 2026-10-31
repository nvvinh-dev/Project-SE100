using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.Net.Http.Headers;

namespace HouseholdFarm.Api.Tests.Features.Auth;

public class AuthTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string LoginPath = "/api/auth/login";
    private const string MePath = "/api/auth/me";

    // Sai số giây giữa lúc test đọc giờ và lúc ứng dụng ghi giờ.
    private static readonly TimeSpan _tolerance = TimeSpan.FromMinutes(1);

    [Fact(DisplayName = "Đăng nhập đúng trả access token chỉ mang sub, jti, exp và đặt cookie refresh token theo D-12")]
    public async Task Login_WithValidCredentials_ReturnsAccessTokenAndRefreshCookie()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        var response = await LoginAsync(household.Owner.PhoneNumber!, TestData.Password, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = (await response.ReadJsonAsync(ct)).GetProperty("data");
        // Body chỉ có access token, refresh token không bao giờ nằm trong body.
        Assert.Equal(["accessToken"], data.EnumerateObject().Select(p => p.Name));

        var token = new JsonWebToken(data.GetProperty("accessToken").GetString());
        Assert.Equal(
            ["aud", "exp", "iss", "jti", "sub"],
            token.Claims.Select(c => c.Type).Order());
        Assert.Equal(household.Owner.Id.ToString(), token.Subject);
        AssertClose(now.AddMinutes(30), token.ValidTo);

        var cookie = Assert.Single(SetCookieHeaderValue.ParseList(response.Headers.GetValues(HeaderNames.SetCookie).ToList()));
        Assert.Equal(RefreshTokenCookie.Name, cookie.Name.Value);
        Assert.True(cookie.HttpOnly);
        Assert.True(cookie.Secure);
        Assert.Equal(Microsoft.Net.Http.Headers.SameSiteMode.Strict, cookie.SameSite);
        Assert.Equal("/api/auth", cookie.Path.Value);
        AssertClose(now.AddDays(7), cookie.Expires!.Value.UtcDateTime);

        // Database chỉ giữ giá trị băm của refresh token.
        var refreshToken = cookie.Value.Value!;
        await using var db = factory.Postgres.CreateDbContext();
        var saved = await db.RefreshTokens.SingleAsync(t => t.UserId == household.Owner.Id, ct);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken))), saved.TokenHash);
        Assert.Null(saved.RevokedAt);
        AssertClose(now.AddDays(7), saved.ExpiresAt);
    }

    [Fact(DisplayName = "Access token nhận được khi đăng nhập dùng được để gọi API")]
    public async Task Login_ReturnedAccessToken_AuthenticatesRequests()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var login = await LoginAsync(household.Member.PhoneNumber!, TestData.Password, ct);
        var accessToken = (await login.ReadJsonAsync(ct)).GetProperty("data").GetProperty("accessToken").GetString();

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.GetAsync(MePath, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(household.Member.Id, (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("id").GetGuid());
    }

    [Fact(DisplayName = "BR-USR-01: đăng nhập được bằng số viết dạng +84, có khoảng trắng")]
    public async Task Login_WithInternationalFormat_NormalizesPhone()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = household.Owner.PhoneNumber!;

        var response = await LoginAsync($"+84 {phone[1..4]} {phone[4..7]} {phone[7..]}", TestData.Password, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "BR-USR-03: sai mật khẩu và sai số điện thoại cùng trả INVALID_CREDENTIALS, cùng câu báo lỗi")]
    public async Task Login_WrongPasswordOrUnknownPhone_ReturnsSameInvalidCredentials()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var wrongPassword = await (await LoginAsync(household.Owner.PhoneNumber!, "SaiMatKhau1", ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        var unknownPhone = await (await LoginAsync("0100000000", TestData.Password, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);

        Assert.Equal(wrongPassword.GetProperty("detail").GetString(), unknownPhone.GetProperty("detail").GetString());
    }

    [Fact(DisplayName = "TC-27: sai mật khẩu 5 lần thì lần thứ 6 nhập đúng vẫn bị TOO_MANY_ATTEMPTS, hết 15 phút thì đăng nhập được")]
    public async Task TC27_Login_AfterFiveWrongPasswords_ReturnsTooManyAttemptsUntilLockoutEnds()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = household.Member.PhoneNumber!;
        var now = TimeProvider.System.GetUtcNow().UtcDateTime;

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await (await LoginAsync(phone, "SaiMatKhau1", ct))
                .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        }

        await (await LoginAsync(phone, TestData.Password, ct))
            .AssertProblemAsync(HttpStatusCode.TooManyRequests, ErrorCodes.TooManyAttempts, ct);

        await using (var db = factory.Postgres.CreateDbContext())
        {
            var user = await db.Users.SingleAsync(u => u.Id == household.Member.Id, ct);
            AssertClose(now.AddMinutes(15), user.LockoutEnd!.Value.UtcDateTime);
            Assert.Equal(0, user.AccessFailedCount);
        }

        // Giả lập đã qua 15 phút: lockout của Identity so với giờ hệ thống nên lùi thời điểm hết khóa tạm về quá khứ.
        await SetLockoutEndAsync(household.Member, TimeProvider.System.GetUtcNow().AddMinutes(-1), ct);

        var afterLockout = await LoginAsync(phone, TestData.Password, ct);
        Assert.Equal(HttpStatusCode.OK, afterLockout.StatusCode);
    }

    [Fact(DisplayName = "BR-USR-15: đăng nhập thành công đưa bộ đếm sai về 0")]
    public async Task Login_Success_ResetsFailedCount()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = household.Worker.PhoneNumber!;

        await LoginAsync(phone, "SaiMatKhau1", ct);
        await LoginAsync(phone, "SaiMatKhau1", ct);
        var response = await LoginAsync(phone, TestData.Password, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.Equal(0, (await db.Users.SingleAsync(u => u.Id == household.Worker.Id, ct)).AccessFailedCount);
    }

    [Fact(DisplayName = "BR-USR-03: tài khoản bị khóa nhập sai mật khẩu vẫn INVALID_CREDENTIALS, đúng mật khẩu mới ACCOUNT_LOCKED")]
    public async Task Login_LockedAccount_ReportsLockOnlyWithCorrectPassword()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await LockAsync(household.Member, ct);

        await (await LoginAsync(household.Member.PhoneNumber!, "SaiMatKhau1", ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidCredentials, ct);
        await (await LoginAsync(household.Member.PhoneNumber!, TestData.Password, ct))
            .AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.AccountLocked, ct);

        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.RefreshTokens.AnyAsync(t => t.UserId == household.Member.Id, ct));
    }

    [Fact(DisplayName = "Thiếu số điện thoại, số sai định dạng, thiếu mật khẩu trả VALIDATION_ERROR theo từng trường")]
    public async Task Login_InvalidInput_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;

        var missing = await (await LoginAsync("", "", ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
        var badPhone = await (await LoginAsync("12345", TestData.Password, ct))
            .AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);

        Assert.Equal(["Vui lòng nhập số điện thoại."], Messages(missing, "phone"));
        Assert.Equal(["Vui lòng nhập mật khẩu."], Messages(missing, "password"));
        Assert.Equal([PhoneNumbers.InvalidMessage], Messages(badPhone, "phone"));
    }

    [Fact(DisplayName = "Gọi API cần đăng nhập mà không có token trả 401 UNAUTHENTICATED")]
    public async Task Me_WithoutToken_ReturnsUnauthenticated()
    {
        var ct = TestContext.Current.CancellationToken;

        var response = await factory.CreateClient().GetAsync(MePath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.Unauthenticated, ct);
    }

    [Fact(DisplayName = "Access token hết hạn hoặc sai chữ ký trả 401 UNAUTHENTICATED")]
    public async Task Me_WithExpiredOrForgedToken_ReturnsUnauthenticated()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var configuration = factory.Services.GetRequiredService<IConfiguration>();
        var expired = new AccessTokenIssuer(
                new JwtSettings(configuration),
                TestClock.At(TimeProvider.System.GetUtcNow().AddHours(-2)))
            .Create(household.Owner.Id);
        var otherKey = new ConfigurationBuilder()
            .AddConfiguration(configuration)
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = new string('k', 48) })
            .Build();
        var forged = new AccessTokenIssuer(new JwtSettings(otherKey), TestClock.At(TimeProvider.System.GetUtcNow()))
            .Create(household.Owner.Id);

        foreach (var token in new[] { expired, forged })
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.GetAsync(MePath, ct);

            await response.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.Unauthenticated, ct);
        }
    }

    [Fact(DisplayName = "/api/auth/me trả đúng vai trò, hộ và ba quyền bổ sung của người đang đăng nhập")]
    public async Task Me_ReturnsRoleHouseholdAndExtraPermissions()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);

        var member = await GetMeAsync(household.MemberWithAllPermissions, ct);
        Assert.Equal(household.MemberWithAllPermissions.Id, member.GetProperty("id").GetGuid());
        Assert.Equal(household.MemberWithAllPermissions.FullName, member.GetProperty("fullName").GetString());
        Assert.Equal(household.MemberWithAllPermissions.PhoneNumber, member.GetProperty("phone").GetString());
        Assert.Equal("Member", member.GetProperty("role").GetString());
        Assert.Equal(household.Household.Code, member.GetProperty("household").GetProperty("code").GetString());
        Assert.Equal(household.Household.Name, member.GetProperty("household").GetProperty("name").GetString());
        Assert.True(member.GetProperty("canManageStock").GetBoolean());
        Assert.True(member.GetProperty("canManageFinance").GetBoolean());
        Assert.True(member.GetProperty("canCloseSeason").GetBoolean());

        var worker = await GetMeAsync(household.Worker, ct);
        Assert.Equal("Worker", worker.GetProperty("role").GetString());
        Assert.False(worker.GetProperty("canManageStock").GetBoolean());
        Assert.False(worker.GetProperty("canManageFinance").GetBoolean());
        Assert.False(worker.GetProperty("canCloseSeason").GetBoolean());

        var systemAdmin = await GetMeAsync(admin, ct);
        Assert.Equal("SystemAdmin", systemAdmin.GetProperty("role").GetString());
        Assert.Equal(JsonValueKind.Null, systemAdmin.GetProperty("household").ValueKind);
    }

    [Fact(DisplayName = "BR-GEN-04: access token cấp trước khi tài khoản bị khóa không dùng tiếp được, trả ACCOUNT_LOCKED")]
    public async Task Me_AfterAccountLocked_ReturnsAccountLocked()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var client = factory.CreateClientFor(household.Member);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(MePath, ct)).StatusCode);

        await LockAsync(household.Member, ct);
        var response = await client.GetAsync(MePath, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.AccountLocked, ct);
    }

    private Task<HttpResponseMessage> LoginAsync(string phone, string password, CancellationToken ct) =>
        factory.CreateClient().PostAsJsonAsync(LoginPath, new { phone, password }, ct);

    private async Task<JsonElement> GetMeAsync(ApplicationUser user, CancellationToken ct)
    {
        var response = await factory.CreateClientFor(user).GetAsync(MePath, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data");
    }

    private async Task LockAsync(ApplicationUser user, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext();
        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.IsLocked, true), ct);
    }

    private async Task SetLockoutEndAsync(ApplicationUser user, DateTimeOffset lockoutEnd, CancellationToken ct)
    {
        await using var db = factory.Postgres.CreateDbContext();
        await db.Users.Where(u => u.Id == user.Id).ExecuteUpdateAsync(s => s.SetProperty(u => u.LockoutEnd, lockoutEnd), ct);
    }

    private static List<string?> Messages(JsonElement problem, string field) =>
        problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()).ToList();

    private static void AssertClose(DateTime expected, DateTime actual) =>
        Assert.InRange(actual, expected - _tolerance, expected + _tolerance);
}
