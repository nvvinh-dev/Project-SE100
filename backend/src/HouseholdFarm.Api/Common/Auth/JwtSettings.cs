using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Cấu hình <c>Jwt:*</c> của phiên đăng nhập (D-10). Đọc lúc dùng chứ không lúc khởi động: máy chưa khai báo khóa ký
/// vẫn chạy được tài liệu API và lệnh <c>dotnet ef</c>, chỉ việc cấp và kiểm tra token báo lỗi.
/// </summary>
public sealed class JwtSettings(IConfiguration configuration)
{
    // Khóa HMAC-SHA256 ngắn hơn 256 bit thì thư viện ký từ chối.
    private const int MinSigningKeyLength = 32;

    public string Issuer => configuration["Jwt:Issuer"] ?? throw Missing("Jwt:Issuer");

    public string Audience => configuration["Jwt:Audience"] ?? throw Missing("Jwt:Audience");

    public TimeSpan AccessTokenLifetime => TimeSpan.FromMinutes(configuration.GetValue<int>("Jwt:AccessTokenMinutes"));

    public TimeSpan RefreshTokenLifetime => TimeSpan.FromDays(configuration.GetValue<int>("Jwt:RefreshTokenDays"));

    /// <summary>Khóa ký access token; ném lỗi kèm cách khai báo khi chưa có hoặc quá ngắn.</summary>
    public SymmetricSecurityKey SigningKey => FindSigningKey() ?? throw new InvalidOperationException(
        $"Thiếu cấu hình Jwt:SigningKey (tối thiểu {MinSigningKeyLength} ký tự). Khai báo trong thư mục "
        + "src/HouseholdFarm.Api bằng: dotnet user-secrets set \"Jwt:SigningKey\" \"<chuỗi ngẫu nhiên>\".");

    /// <summary>Khóa ký access token, hoặc <c>null</c> khi chưa khai báo hay quá ngắn.</summary>
    public SymmetricSecurityKey? FindSigningKey() =>
        configuration["Jwt:SigningKey"] is { Length: >= MinSigningKeyLength } key
            ? new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
            : null;

    private static InvalidOperationException Missing(string key) => new($"Thiếu cấu hình {key}.");
}
