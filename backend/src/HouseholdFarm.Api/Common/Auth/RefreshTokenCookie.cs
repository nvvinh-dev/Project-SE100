namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Cookie <c>refresh_token</c> theo D-12: JavaScript không đọc được, chỉ gửi kèm yêu cầu tới <c>/api/auth</c>.
/// Controller ghi cookie vì đây là chi tiết của HTTP; service không đụng tới <c>HttpContext</c>.
/// </summary>
public sealed class RefreshTokenCookie(IConfiguration configuration)
{
    public const string Name = "refresh_token";

    /// <summary>Đường dẫn của cookie; trình duyệt chỉ gửi cookie kèm yêu cầu dưới đường dẫn này.</summary>
    public const string Path = "/api/auth";

    /// <summary>Refresh token trình duyệt gửi kèm, hoặc <c>null</c> khi không có cookie.</summary>
    public static string? Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var token) && !string.IsNullOrEmpty(token) ? token : null;

    public void Write(HttpResponse response, string token, DateTime expiresAt)
    {
        var options = CreateOptions();
        // Có hạn để cookie còn sau khi đóng trình duyệt: mở lại ứng dụng trong hạn phiên không phải đăng nhập lại (FR-AUTH-03).
        options.Expires = expiresAt;
        response.Cookies.Append(Name, token, options);
    }

    /// <summary>Xóa cookie khi phiên kết thúc (đăng xuất, đổi mật khẩu).</summary>
    // Trình duyệt chỉ xóa cookie khi đường dẫn và các thuộc tính khớp với lúc đặt.
    public void Delete(HttpResponse response) => response.Cookies.Delete(Name, CreateOptions());

    private CookieOptions CreateOptions() => new()
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        // Tắt được bằng cấu hình chỉ khi demo trên điện thoại thật qua HTTP (D-14).
        Secure = configuration.GetValue("Auth:RefreshCookieSecure", defaultValue: true),
    };
}
