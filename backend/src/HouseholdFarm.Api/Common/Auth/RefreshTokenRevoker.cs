using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Kết thúc mọi phiên đăng nhập của một người (BR-USR-04). Đổi mật khẩu, khóa tài khoản, đổi vai trò, đặt lại mật khẩu
/// gọi hàm này.
/// </summary>
/// <remarks>
/// Scoped để dùng chung <see cref="AppDbContext"/> với service gọi nó: gọi trong giao dịch database đang mở thì việc thu hồi
/// nằm trong giao dịch đó.
/// </remarks>
public sealed class RefreshTokenRevoker(AppDbContext db, IAppClock clock)
{
    /// <summary>Thu hồi mọi refresh token còn hiệu lực của <paramref name="userId"/>, kể cả token của phiên đang dùng.</summary>
    public Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;

        // Ghi thẳng xuống database, không qua SaveChangesAsync, để không phụ thuộc token nào đang được theo dõi.
        return db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }
}
