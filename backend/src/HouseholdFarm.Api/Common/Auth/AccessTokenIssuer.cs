using HouseholdFarm.Api.Common.Time;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>Cấp access token JWT ngắn hạn (D-10).</summary>
public sealed class AccessTokenIssuer(JwtSettings settings, IAppClock clock)
{
    // Tắt thời điểm mặc định (iat, nbf) vì thư viện lấy từ giờ hệ thống; hạn token tự tính từ IAppClock.
    private static readonly JsonWebTokenHandler _handler = new() { SetDefaultTimesOnTokenCreation = false };

    /// <summary>
    /// D-11: token chỉ mang mã người dùng (<c>sub</c>), <c>jti</c>, <c>exp</c>, kèm <c>iss</c>, <c>aud</c> để chỉ ứng dụng này
    /// chấp nhận. Hộ, vai trò, quyền đọc lại từ database mỗi yêu cầu để thay đổi có hiệu lực ngay.
    /// </summary>
    public string Create(Guid userId) => _handler.CreateToken(new SecurityTokenDescriptor
    {
        Issuer = settings.Issuer,
        Audience = settings.Audience,
        Expires = clock.UtcNow + settings.AccessTokenLifetime,
        Claims = new Dictionary<string, object>
        {
            [JwtRegisteredClaimNames.Sub] = userId.ToString(),
            [JwtRegisteredClaimNames.Jti] = Guid.CreateVersion7().ToString(),
        },
        SigningCredentials = new SigningCredentials(settings.SigningKey, SecurityAlgorithms.HmacSha256),
    });
}
