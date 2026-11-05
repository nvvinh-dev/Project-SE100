using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Kết thúc mọi phiên đăng nhập của một người (BR-USR-04). Đổi mật khẩu, khóa tài khoản, đổi vai trò, đặt lại mật khẩu
/// gọi hàm này.
/// </summary>
/// <remarks>
/// Mọi thao tác cấp hoặc thu hồi refresh token khóa dòng người dùng trước (<see cref="LockUserAsync"/>), rồi mới đụng tới
/// <c>refresh_tokens</c>. Nhờ vậy thao tác thu hồi không bỏ sót token do một yêu cầu làm mới phiên chạy cùng lúc vừa cấp,
/// và các thao tác khóa theo cùng một thứ tự nên không bị khóa chéo.
/// Scoped để dùng chung <see cref="AppDbContext"/> với service gọi nó.
/// </remarks>
public sealed class RefreshTokenRevoker(AppDbContext db, IAppClock clock)
{
    /// <summary>
    /// Khóa dòng <c>asp_net_users</c> của <paramref name="userId"/> tới hết giao dịch database đang mở.
    /// Gọi ở đầu thao tác, trước khi đọc dữ liệu để kiểm tra.
    /// </summary>
    /// <remarks>
    /// <c>FOR NO KEY UPDATE</c> chặn thao tác khác cùng khóa người này nhưng không chặn khóa ngoại của bảng khác trỏ tới
    /// người dùng (khóa <c>FOR KEY SHARE</c> khi thêm dòng <c>refresh_tokens</c>, <c>audit_logs</c>...).
    /// </remarks>
    public Task LockUserAsync(Guid userId, CancellationToken ct)
    {
        EnsureInTransaction();
        return db.Database.ExecuteSqlAsync($"SELECT 1 FROM asp_net_users WHERE id = {userId} FOR NO KEY UPDATE", ct);
    }

    /// <summary>
    /// Thu hồi mọi refresh token còn hiệu lực của <paramref name="userId"/>, kể cả token của phiên đang dùng.
    /// Phải gọi trong giao dịch database đang mở; hàm tự khóa dòng người dùng nếu bên gọi chưa khóa.
    /// </summary>
    public async Task RevokeAllAsync(Guid userId, CancellationToken ct)
    {
        // Khóa lại trong cùng giao dịch không chờ gì; bên gọi quên khóa thì vẫn đúng thứ tự người dùng trước, token sau.
        await LockUserAsync(userId, ct);

        var now = clock.UtcNow;
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    // Khóa dòng ngoài giao dịch database thì nhả ngay sau câu lệnh, không bảo vệ được gì.
    private void EnsureInTransaction()
    {
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Khóa dòng người dùng phải nằm trong giao dịch database đang mở.");
        }
    }
}
