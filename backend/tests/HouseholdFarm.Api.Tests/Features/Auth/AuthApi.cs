using System.Net;
using System.Net.Http.Json;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Net.Http.Headers;

namespace HouseholdFarm.Api.Tests.Features.Auth;

/// <summary>Một phiên đăng nhập trên một thiết bị: access token trong bộ nhớ, refresh token trong cookie.</summary>
internal sealed record TestSession(string AccessToken, string RefreshToken);

/// <summary>
/// Gọi các API xác thực và gửi cookie <c>refresh_token</c> bằng tay. Cookie có thuộc tính <c>Secure</c> nên client của
/// test (http://localhost) không tự gửi lại, mỗi test tự giữ refresh token của từng phiên.
/// </summary>
internal static class AuthApi
{
    public const string RegisterPath = "/api/auth/register";
    public const string LoginPath = "/api/auth/login";
    public const string RefreshPath = "/api/auth/refresh";
    public const string LogoutPath = "/api/auth/logout";
    public const string ChangePasswordPath = "/api/auth/change-password";
    public const string MePath = "/api/auth/me";

    public static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });

    /// <summary>Đăng nhập thành công, trả phiên vừa bắt đầu.</summary>
    public static async Task<TestSession> LoginAsync(
        WebApplicationFactory<Program> factory,
        string phone,
        string password,
        CancellationToken ct)
    {
        var response = await CreateClient(factory).PostAsJsonAsync(LoginPath, new { phone, password }, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadSessionAsync(response, ct);
    }

    public static Task<HttpResponseMessage> RefreshAsync(
        WebApplicationFactory<Program> factory,
        string? refreshToken,
        CancellationToken ct) =>
        SendWithCookieAsync(factory, RefreshPath, refreshToken, ct);

    public static Task<HttpResponseMessage> LogoutAsync(
        WebApplicationFactory<Program> factory,
        string? refreshToken,
        CancellationToken ct) =>
        SendWithCookieAsync(factory, LogoutPath, refreshToken, ct);

    /// <summary>Access token trong body và refresh token trong cookie của phản hồi đăng nhập, làm mới phiên.</summary>
    public static async Task<TestSession> ReadSessionAsync(HttpResponseMessage response, CancellationToken ct)
    {
        var accessToken = (await response.ReadJsonAsync(ct)).GetProperty("data").GetProperty("accessToken").GetString()!;
        return new TestSession(accessToken, ReadRefreshCookie(response)!.Value.Value!);
    }

    /// <summary>Cookie <c>refresh_token</c> mà phản hồi đặt, hoặc <c>null</c> khi phản hồi không đặt cookie.</summary>
    public static SetCookieHeaderValue? ReadRefreshCookie(HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).SingleOrDefault(c => c.Name.Value == RefreshTokenCookie.Name)
            : null;

    private static Task<HttpResponseMessage> SendWithCookieAsync(
        WebApplicationFactory<Program> factory,
        string path,
        string? refreshToken,
        CancellationToken ct)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{RefreshTokenCookie.Name}={refreshToken}");
        }

        return CreateClient(factory).SendAsync(request, ct);
    }
}
