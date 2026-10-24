using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Entities;

namespace HouseholdFarm.Api.Common.Persistence;

public static class UserQueryExtensions
{
    /// <summary>
    /// Chỉ người dùng cùng hộ với người đang đăng nhập. <c>asp_net_users</c> không có Global Query Filter, nên mọi truy vấn lấy
    /// người dùng trong hộ đi qua hàm này; người dùng của hộ khác được xử lý như không tồn tại (BR-GEN-02).
    /// </summary>
    /// <remarks>
    /// Chỉ luồng xác thực, nạp <c>ICurrentUser</c>, kiểm tra trùng số điện thoại (BR-USR-02) và module ADM đọc <c>Users</c>
    /// không qua hàm này.
    /// </remarks>
    public static IQueryable<ApplicationUser> InCurrentHousehold(this IQueryable<ApplicationUser> users, ICurrentUser currentUser)
    {
        // Không thuộc hộ nào (Quản trị hệ thống, chưa đăng nhập) thì không thấy ai; so HouseholdId với null sẽ ra mọi Quản trị.
        if (currentUser.HouseholdId is not { } householdId)
        {
            return users.Where(_ => false);
        }

        return users.Where(u => u.HouseholdId == householdId);
    }
}
