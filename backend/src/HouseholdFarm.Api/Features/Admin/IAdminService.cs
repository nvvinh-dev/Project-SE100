using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Admin;

/// <summary>
/// Quản trị tài khoản toàn hệ thống (FR-ADM-01..03), chỉ Quản trị hệ thống. Chỉ đọc bảng tài khoản và hộ, không đọc bảng nghiệp vụ.
/// </summary>
/// <remarks>
/// Chỉ áp dụng cho tài khoản thuộc hộ: tài khoản Quản trị hệ thống được xử lý như không tồn tại (BR-USR-06), nên Quản trị
/// cũng không tự khóa được chính mình.
/// </remarks>
public interface IAdminService
{
    /// <summary>Danh sách tài khoản của mọi hộ. Lỗi: <c>FORBIDDEN</c>.</summary>
    Task<PagedResult<AdminAccountResponse>> GetAccountsAsync(AdminAccountQuery query, CancellationToken ct);

    /// <summary>Chi tiết một tài khoản. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<AdminAccountResponse> GetAccountAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Khóa tài khoản, kết thúc mọi phiên, ghi nhật ký vào hộ của tài khoản đó. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.
    /// </summary>
    Task<AdminAccountResponse> LockAsync(Guid id, CancellationToken ct);

    /// <summary>Mở khóa tài khoản, ghi nhật ký vào hộ của tài khoản đó. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<AdminAccountResponse> UnlockAsync(Guid id, CancellationToken ct);

    /// <summary>
    /// Đặt mật khẩu mới cho tài khoản Chủ hộ, kết thúc mọi phiên. Thành viên quên mật khẩu do Chủ hộ đặt lại (FR-HH-09).
    /// Lỗi: <c>FORBIDDEN</c> (tài khoản không phải Chủ hộ), <c>NOT_FOUND</c>.
    /// </summary>
    Task<AdminAccountResponse> ResetPasswordAsync(Guid id, ResetAccountPasswordRequest request, CancellationToken ct);
}
