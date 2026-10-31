namespace HouseholdFarm.Api.Features.Auth;

public interface IAuthService
{
    /// <summary>
    /// Đăng nhập bằng số điện thoại và mật khẩu, bắt đầu một phiên đăng nhập.
    /// Lỗi: <c>INVALID_CREDENTIALS</c>, <c>TOO_MANY_ATTEMPTS</c>, <c>ACCOUNT_LOCKED</c>.
    /// </summary>
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>Thông tin của người đang đăng nhập.</summary>
    Task<CurrentUserResponse> GetMeAsync(CancellationToken ct);
}
