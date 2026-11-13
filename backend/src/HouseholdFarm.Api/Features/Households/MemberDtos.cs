using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Households;

// Enum bắt buộc khai báo nullable: thiếu trường trong JSON thì nhận null và validator báo thiếu,
// không thành giá trị 0 của enum (Owner) rồi bị hiểu nhầm là chọn Chủ hộ.

/// <summary>Chủ hộ tạo tài khoản thành viên (FR-HH-02). Ba quyền bổ sung mặc định tắt.</summary>
/// <param name="Role">Chỉ <c>Member</c> hoặc <c>Worker</c> (BR-USR-08).</param>
/// <param name="InitialPassword">Mật khẩu ban đầu theo D-22; Chủ hộ tự báo cho thành viên.</param>
public sealed record CreateMemberRequest(
    string FullName,
    string Phone,
    UserRole? Role,
    string InitialPassword);

/// <summary>Sửa họ tên, số điện thoại của thành viên (FR-HH-04). Đổi vai trò, quyền bổ sung là thao tác riêng.</summary>
/// <param name="Version">Giá trị <c>version</c> nhận được khi đọc thành viên (BR-GEN-11).</param>
public sealed record UpdateMemberRequest(
    string FullName,
    string Phone,
    uint Version);

/// <summary>Đổi vai trò qua lại giữa <c>Member</c> và <c>Worker</c> (thao tác <c>ChangeRole</c>, BR-USR-08).</summary>
public sealed record ChangeMemberRoleRequest(
    UserRole? Role,
    uint Version);

/// <summary>Bật, tắt ba quyền bổ sung của Thành viên hộ (thao tác <c>ChangePermissions</c>, FR-HH-07).</summary>
public sealed record ChangeMemberPermissionsRequest(
    bool CanManageStock,
    bool CanManageFinance,
    bool CanCloseSeason,
    uint Version);

/// <summary>Chủ hộ đặt mật khẩu mới cho thành viên (thao tác <c>ResetPassword</c>, FR-HH-09).</summary>
public sealed record ResetMemberPasswordRequest(string NewPassword);

/// <summary>Chi tiết một người trong hộ, chỉ Chủ hộ xem.</summary>
/// <param name="IsLocked">Khóa do Chủ hộ thực hiện, khác khóa tạm do đăng nhập sai (BR-USR-15).</param>
public sealed record MemberResponse(
    Guid Id,
    string FullName,
    string Phone,
    UserRole Role,
    bool CanManageStock,
    bool CanManageFinance,
    bool CanCloseSeason,
    bool IsLocked,
    uint Version);

/// <summary>Một dòng của danh sách người trong hộ.</summary>
/// <remarks>
/// Thành viên hộ chỉ thấy họ tên và vai trò (ma trận phân quyền, ghi chú 1): với Thành viên hộ các trường còn lại để trống.
/// </remarks>
public sealed record MemberListItem(
    Guid Id,
    string FullName,
    string? Phone,
    UserRole Role,
    bool? CanManageStock,
    bool? CanManageFinance,
    bool? CanCloseSeason,
    bool? IsLocked);

/// <summary>
/// Tham số của danh sách người trong hộ: <c>search</c> tìm theo họ tên, số điện thoại; sắp xếp theo
/// <see cref="SortFields"/>, mặc định theo họ tên.
/// </summary>
public sealed record MemberQuery : PageQuery
{
    /// <summary>Các trường sắp xếp được, tên camelCase như trong phản hồi.</summary>
    public static readonly IReadOnlyList<string> SortFields = ["fullName", "createdAt"];

    public UserRole? Role { get; init; }

    public bool? IsLocked { get; init; }
}
