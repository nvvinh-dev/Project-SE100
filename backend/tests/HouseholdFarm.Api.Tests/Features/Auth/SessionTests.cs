using System.Net;
using System.Net.Http.Headers;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Features.Auth;

/// <summary>Làm mới phiên (FR-AUTH-03), đăng xuất (FR-AUTH-04), chặn tài khoản bị khóa khi làm mới phiên (FR-AUTH-06).</summary>
public class SessionTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    [Fact(DisplayName = "FR-AUTH-03: làm mới phiên trả access token mới, đổi cookie, thu hồi token cũ và ghi token thay thế")]
    public async Task Refresh_WithValidCookie_RotatesRefreshToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var login = await LoginAsync(household.Member, ct);

        var response = await AuthApi.RefreshAsync(factory, login.RefreshToken, ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var refreshed = await AuthApi.ReadSessionAsync(response, ct);
        Assert.NotEqual(login.RefreshToken, refreshed.RefreshToken);
        var cookie = AuthApi.ReadRefreshCookie(response)!;
        Assert.True(cookie.HttpOnly);
        Assert.Equal(RefreshTokenCookie.Path, cookie.Path.Value);

        await using var db = factory.Postgres.CreateDbContext();
        var tokens = await TokensOfAsync(db, household.Member, ct);
        Assert.Equal(2, tokens.Count);
        var (old, replacement) = (tokens[0], tokens[1]);
        Assert.NotNull(old.RevokedAt);
        Assert.Equal(replacement.Id, old.ReplacedById);
        Assert.Null(replacement.RevokedAt);

        // Access token mới dùng được, token mới làm mới tiếp được.
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(AuthApi.MePath, ct)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(factory, refreshed.RefreshToken, ct)).StatusCode);
    }

    [Fact(DisplayName = "BR-USR-05: dùng lại token đã thu hồi trả INVALID_REFRESH_TOKEN và thu hồi mọi token của người đó")]
    public async Task Refresh_WithRevokedToken_RevokesAllTokensOfUser()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = await LoginAsync(household.Member, ct);
        var laptop = await LoginAsync(household.Member, ct);
        var refreshed = await AuthApi.ReadSessionAsync(await AuthApi.RefreshAsync(factory, phone.RefreshToken, ct), ct);

        var reuse = await AuthApi.RefreshAsync(factory, phone.RefreshToken, ct);

        await reuse.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        await using (var db = factory.Postgres.CreateDbContext())
        {
            Assert.All(await TokensOfAsync(db, household.Member, ct), t => Assert.NotNull(t.RevokedAt));
        }

        foreach (var token in new[] { refreshed.RefreshToken, laptop.RefreshToken })
        {
            await (await AuthApi.RefreshAsync(factory, token, ct))
                .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        }
    }

    [Fact(DisplayName = "BR-USR-04: refresh token hết hạn trả INVALID_REFRESH_TOKEN")]
    public async Task Refresh_WithExpiredToken_ReturnsInvalidRefreshToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var login = await LoginAsync(household.Member, ct);

        // Ứng dụng chạy ở thời điểm sau hạn 7 ngày của refresh token (D-10).
        var eightDaysLater = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddSingleton<TimeProvider>(new FixedTimeProvider(TimeProvider.System.GetUtcNow().AddDays(8)))));

        var response = await AuthApi.RefreshAsync(eightDaysLater, login.RefreshToken, ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
    }

    [Fact(DisplayName = "BR-USR-04: không có cookie hoặc token không có trong database trả INVALID_REFRESH_TOKEN")]
    public async Task Refresh_WithoutOrUnknownToken_ReturnsInvalidRefreshToken()
    {
        var ct = TestContext.Current.CancellationToken;

        foreach (var token in new[] { null, "khong-phai-token" })
        {
            await (await AuthApi.RefreshAsync(factory, token, ct))
                .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
        }
    }

    [Fact(DisplayName = "FR-AUTH-06: tài khoản bị khóa làm mới phiên trả INVALID_REFRESH_TOKEN")]
    public async Task Refresh_LockedAccount_ReturnsInvalidRefreshToken()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var login = await LoginAsync(household.Member, ct);
        await using (var db = factory.Postgres.CreateDbContext())
        {
            await db.Users.Where(u => u.Id == household.Member.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.IsLocked, true), ct);
        }

        var response = await AuthApi.RefreshAsync(factory, login.RefreshToken, ct);

        await response.AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
    }

    [Fact(DisplayName = "FR-AUTH-04: đăng xuất thu hồi token của phiên này, xóa cookie; thiết bị khác vẫn làm mới phiên được")]
    public async Task Logout_RevokesOnlyCurrentSession()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var phone = await LoginAsync(household.Worker, ct);
        var laptop = await LoginAsync(household.Worker, ct);

        var response = await AuthApi.LogoutAsync(factory, phone.RefreshToken, ct);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var cookie = AuthApi.ReadRefreshCookie(response)!;
        Assert.Equal(string.Empty, cookie.Value.Value);
        Assert.Equal(RefreshTokenCookie.Path, cookie.Path.Value);
        Assert.True(cookie.Expires < DateTimeOffset.UtcNow);

        // Kiểm tra thiết bị khác trước: gửi lại token đã thu hồi sẽ thu hồi mọi phiên theo BR-USR-05.
        Assert.Equal(HttpStatusCode.OK, (await AuthApi.RefreshAsync(factory, laptop.RefreshToken, ct)).StatusCode);
        await (await AuthApi.RefreshAsync(factory, phone.RefreshToken, ct))
            .AssertProblemAsync(HttpStatusCode.Unauthorized, ErrorCodes.InvalidRefreshToken, ct);
    }

    [Fact(DisplayName = "FR-AUTH-04: đăng xuất không có cookie hoặc bằng token đã thu hồi vẫn trả 204")]
    public async Task Logout_WithoutOrRevokedToken_ReturnsNoContent()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var login = await LoginAsync(household.Worker, ct);
        Assert.Equal(HttpStatusCode.NoContent, (await AuthApi.LogoutAsync(factory, login.RefreshToken, ct)).StatusCode);

        foreach (var token in new[] { null, login.RefreshToken })
        {
            Assert.Equal(HttpStatusCode.NoContent, (await AuthApi.LogoutAsync(factory, token, ct)).StatusCode);
        }
    }

    private Task<TestSession> LoginAsync(ApplicationUser user, CancellationToken ct) =>
        AuthApi.LoginAsync(factory, user.PhoneNumber!, TestData.Password, ct);

    private static Task<List<RefreshToken>> TokensOfAsync(
        Api.Data.AppDbContext db,
        ApplicationUser user,
        CancellationToken ct) =>
        db.RefreshTokens.Where(t => t.UserId == user.Id).OrderBy(t => t.CreatedAt).ToListAsync(ct);
}
