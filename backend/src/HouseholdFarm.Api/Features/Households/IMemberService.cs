using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Households;

/// <summary>
/// Tài khoản người trong hộ (FR-HH-02..07, FR-HH-09). Không có API xóa tài khoản: khóa để thu hồi quyền truy cập mà vẫn giữ lịch sử.
/// </summary>
/// <remarks>
/// Các thao tác theo id chỉ nhắm được <c>Member</c>, <c>Worker</c> của hộ: người của hộ khác báo <c>NOT_FOUND</c>;
/// Chủ hộ hoặc chính mình báo <c>FORBIDDEN</c> (BR-USR-07).
/// </remarks>
public interface IMemberService
{
    /// <summary>
    /// Danh sách người trong hộ. Chủ hộ thấy cả hộ; Thành viên hộ chỉ thấy Chủ hộ và người cùng mùa vụ với mình, chỉ họ tên và
    /// vai trò (ma trận phân quyền, ghi chú 1). Lỗi: <c>FORBIDDEN</c> (Nhân công, Quản trị).
    /// </summary>
    Task<PagedResult<MemberListItem>> GetListAsync(MemberQuery query, CancellationToken ct);

    /// <summary>Chi tiết một người trong hộ, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<MemberResponse> GetAsync(Guid id, CancellationToken ct);

    /// <summary>Tạo tài khoản thành viên, chỉ Chủ hộ. Lỗi: <c>FORBIDDEN</c>, <c>PHONE_ALREADY_USED</c>.</summary>
    Task<MemberResponse> CreateAsync(CreateMemberRequest request, CancellationToken ct);

    /// <summary>
    /// Sửa họ tên, số điện thoại. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>PHONE_ALREADY_USED</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<MemberResponse> UpdateAsync(Guid id, UpdateMemberRequest request, CancellationToken ct);

    /// <summary>
    /// Đổi vai trò; kết thúc mọi phiên của thành viên, sang Nhân công thì tắt quyền bổ sung, ghi nhật ký.
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<MemberResponse> ChangeRoleAsync(Guid id, ChangeMemberRoleRequest request, CancellationToken ct);

    /// <summary>
    /// Bật, tắt quyền bổ sung, ghi nhật ký. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>VALIDATION_ERROR</c> (bật cho Nhân công),
    /// <c>CONCURRENCY_CONFLICT</c>.
    /// </summary>
    Task<MemberResponse> ChangePermissionsAsync(Guid id, ChangeMemberPermissionsRequest request, CancellationToken ct);

    /// <summary>Khóa tài khoản, kết thúc mọi phiên, ghi nhật ký. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<MemberResponse> LockAsync(Guid id, CancellationToken ct);

    /// <summary>Mở khóa tài khoản, ghi nhật ký. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<MemberResponse> UnlockAsync(Guid id, CancellationToken ct);

    /// <summary>Đặt mật khẩu mới, kết thúc mọi phiên. Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>.</summary>
    Task<MemberResponse> ResetPasswordAsync(Guid id, ResetMemberPasswordRequest request, CancellationToken ct);
}
