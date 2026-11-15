using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Admin;

/// <summary>
/// Một tài khoản thuộc hộ, ở mức thông tin tài khoản (FR-ADM-01): không có dữ liệu sản xuất, tài chính.
/// Tài khoản Quản trị hệ thống không có trong danh sách (BR-USR-06).
/// </summary>
/// <param name="IsLocked">Khóa do Chủ hộ hoặc Quản trị thực hiện, khác khóa tạm do đăng nhập sai (BR-USR-15).</param>
public sealed record AdminAccountResponse(
    Guid Id,
    string FullName,
    string Phone,
    UserRole Role,
    string HouseholdCode,
    string HouseholdName,
    bool IsLocked,
    DateTime CreatedAt);

/// <summary>
/// Tham số danh sách tài khoản: <c>search</c> tìm theo họ tên, số điện thoại, mã hộ, tên hộ; sắp xếp theo
/// <see cref="SortFields"/>, mặc định theo họ tên.
/// </summary>
public sealed record AdminAccountQuery : PageQuery
{
    /// <summary>Các trường sắp xếp được, tên camelCase như trong phản hồi.</summary>
    public static readonly IReadOnlyList<string> SortFields = ["fullName", "householdCode", "createdAt"];

    public UserRole? Role { get; init; }

    public bool? IsLocked { get; init; }
}

/// <summary>Quản trị đặt mật khẩu mới cho Chủ hộ quên mật khẩu (thao tác <c>ResetPassword</c>, FR-ADM-03).</summary>
public sealed record ResetAccountPasswordRequest(string NewPassword);
