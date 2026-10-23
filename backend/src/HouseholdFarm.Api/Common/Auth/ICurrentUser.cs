using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Người dùng của yêu cầu hiện tại, nạp từ database một lần mỗi yêu cầu (D-11) để khóa tài khoản, đổi vai trò,
/// đổi quyền, đổi phân công có hiệu lực ngay. Global Query Filter lấy hộ từ đây.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Đã đăng nhập. Khi <c>false</c>, không đọc <see cref="UserId"/> và <see cref="Role"/>.</summary>
    bool IsAuthenticated { get; }

    Guid UserId { get; }

    /// <summary>Hộ của người dùng; để trống với Quản trị hệ thống và khi chưa đăng nhập.</summary>
    Guid? HouseholdId { get; }

    UserRole Role { get; }

    /// <summary>Quyền bổ sung <c>ManageStock</c>; chỉ có thể <c>true</c> với Thành viên hộ.</summary>
    bool CanManageStock { get; }

    /// <summary>Quyền bổ sung <c>ManageFinance</c>; chỉ có thể <c>true</c> với Thành viên hộ.</summary>
    bool CanManageFinance { get; }

    /// <summary>Quyền bổ sung <c>CloseSeason</c>; chỉ có thể <c>true</c> với Thành viên hộ.</summary>
    bool CanCloseSeason { get; }

    /// <summary>Các mùa vụ người dùng được phân công, tức phạm vi làm việc.</summary>
    IReadOnlyList<Guid> AssignedSeasonIds { get; }
}
