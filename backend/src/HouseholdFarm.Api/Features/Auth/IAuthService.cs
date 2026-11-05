namespace HouseholdFarm.Api.Features.Auth;

public interface IAuthService
{
    /// <summary>
    /// Đăng ký Chủ hộ và tạo hộ mới trong một giao dịch database. Chưa bắt đầu phiên đăng nhập (UC-AUTH-01).
    /// Lỗi: <c>PHONE_ALREADY_USED</c>.
    /// </summary>
    Task<CurrentUserResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);

    /// <summary>
    /// Đăng nhập bằng số điện thoại và mật khẩu, bắt đầu một phiên đăng nhập.
    /// Lỗi: <c>INVALID_CREDENTIALS</c>, <c>TOO_MANY_ATTEMPTS</c>, <c>ACCOUNT_LOCKED</c>.
    /// </summary>
    Task<AuthSession> LoginAsync(LoginRequest request, CancellationToken ct);

    /// <summary>
    /// Làm mới phiên: thu hồi refresh token đang dùng, cấp access token và refresh token mới (BR-USR-05).
    /// Lỗi: <c>INVALID_REFRESH_TOKEN</c>.
    /// </summary>
    /// <param name="refreshToken">Giá trị trong cookie, <c>null</c> khi trình duyệt không gửi cookie.</param>
    Task<AuthSession> RefreshAsync(string? refreshToken, CancellationToken ct);

    /// <summary>
    /// Kết thúc phiên trên thiết bị này: thu hồi đúng refresh token của phiên. Không có token hoặc token đã thu hồi thì
    /// không làm gì.
    /// </summary>
    Task LogoutAsync(string? refreshToken, CancellationToken ct);

    /// <summary>
    /// Đổi mật khẩu của chính mình, rồi thu hồi mọi refresh token của người đó (BR-USR-17).
    /// Lỗi: <c>INVALID_CREDENTIALS</c>.
    /// </summary>
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);

    /// <summary>Thông tin của người đang đăng nhập.</summary>
    Task<CurrentUserResponse> GetMeAsync(CancellationToken ct);

    /// <summary>Sửa họ tên, số điện thoại của chính mình. Lỗi: <c>PHONE_ALREADY_USED</c>.</summary>
    Task<CurrentUserResponse> UpdateProfileAsync(UpdateProfileRequest request, CancellationToken ct);
}
