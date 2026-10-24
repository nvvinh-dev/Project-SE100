using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Infrastructure;

namespace HouseholdFarm.Api.Tests.Common.Auth;

public class CurrentUserPermissionExtensionsTests
{
    private static readonly Guid _householdId = Guid.CreateVersion7();

    private static readonly Dictionary<string, TestCurrentUser> _users = new()
    {
        ["Owner"] = User(UserRole.Owner),
        ["Member"] = User(UserRole.Member),
        ["MemberStock"] = User(UserRole.Member, canManageStock: true),
        ["MemberFinance"] = User(UserRole.Member, canManageFinance: true),
        ["MemberClose"] = User(UserRole.Member, canCloseSeason: true),
        ["Worker"] = User(UserRole.Worker),
        ["SystemAdmin"] = TestCurrentUser.SystemAdmin(),
    };

    private static readonly Dictionary<string, Action<ICurrentUser>> _ensures = new()
    {
        [nameof(CurrentUserPermissionExtensions.EnsureOwner)] = u => u.EnsureOwner(),
        [nameof(CurrentUserPermissionExtensions.EnsureNotWorker)] = u => u.EnsureNotWorker(),
        [nameof(CurrentUserPermissionExtensions.EnsureCanManageStock)] = u => u.EnsureCanManageStock(),
        [nameof(CurrentUserPermissionExtensions.EnsureCanManageFinance)] = u => u.EnsureCanManageFinance(),
        [nameof(CurrentUserPermissionExtensions.EnsureCanCloseSeason)] = u => u.EnsureCanCloseSeason(),
        [nameof(CurrentUserPermissionExtensions.EnsureSystemAdmin)] = u => u.EnsureSystemAdmin(),
    };

    // Ma trận phân quyền: ai qua được từng hàm kiểm tra.
    [Theory(DisplayName = "BR-GEN-03: hàm kiểm tra quyền cho qua đúng vai trò, quyền bổ sung theo ma trận phân quyền")]
    [InlineData("EnsureOwner", "Owner")]
    [InlineData("EnsureNotWorker", "Owner", "Member", "MemberStock", "MemberFinance", "MemberClose")]
    [InlineData("EnsureCanManageStock", "Owner", "MemberStock")]
    [InlineData("EnsureCanManageFinance", "Owner", "MemberFinance")]
    [InlineData("EnsureCanCloseSeason", "Owner", "MemberClose")]
    [InlineData("EnsureSystemAdmin", "SystemAdmin")]
    public void Ensure_AllowsOnlyRolesInMatrix(string ensure, params string[] allowedUsers)
    {
        foreach (var (name, user) in _users)
        {
            if (allowedUsers.Contains(name))
            {
                _ensures[ensure](user);
                continue;
            }

            var ex = Assert.Throws<AppException>(() => _ensures[ensure](user));
            Assert.Equal(ErrorCodes.Forbidden, ex.Code);
        }
    }

    [Fact(DisplayName = "Hàm kiểm tra quyền với người chưa đăng nhập trả UNAUTHENTICATED")]
    public void Ensure_WhenAnonymous_ThrowsUnauthenticated()
    {
        Assert.All(_ensures.Values, ensure =>
        {
            var ex = Assert.Throws<AppException>(() => ensure(TestCurrentUser.Anonymous));
            Assert.Equal(ErrorCodes.Unauthenticated, ex.Code);
        });
    }

    [Fact(DisplayName = "Chủ hộ có mọi quyền bổ sung; Nhân công, Quản trị, người chưa đăng nhập không có quyền nào")]
    public void HasPermission_OwnerAlways_WorkerAdminAnonymousNever()
    {
        // Cờ quyền bật nhầm trên tài khoản không phải Member vẫn không được tính (BR-USR-09).
        var workerWithFlags = new TestCurrentUser
        {
            IsAuthenticated = true,
            UserId = Guid.CreateVersion7(),
            HouseholdId = _householdId,
            Role = UserRole.Worker,
            CanManageStock = true,
            CanManageFinance = true,
            CanCloseSeason = true,
        };

        Assert.All(Enum.GetValues<ExtraPermission>(), permission =>
        {
            Assert.True(_users["Owner"].HasPermission(permission));
            Assert.False(workerWithFlags.HasPermission(permission));
            Assert.False(_users["SystemAdmin"].HasPermission(permission));
            Assert.False(TestCurrentUser.Anonymous.HasPermission(permission));
        });
    }

    [Fact(DisplayName = "Thành viên hộ chỉ có quyền bổ sung được bật cho mình")]
    public void HasPermission_Member_FollowsOwnFlags()
    {
        Assert.False(_users["Member"].HasPermission(ExtraPermission.ManageStock));
        Assert.True(_users["MemberStock"].HasPermission(ExtraPermission.ManageStock));
        Assert.False(_users["MemberStock"].HasPermission(ExtraPermission.ManageFinance));
        Assert.True(_users["MemberFinance"].HasPermission(ExtraPermission.ManageFinance));
        Assert.False(_users["MemberFinance"].HasPermission(ExtraPermission.CloseSeason));
        Assert.True(_users["MemberClose"].HasPermission(ExtraPermission.CloseSeason));
    }

    private static TestCurrentUser User(
        UserRole role,
        bool canManageStock = false,
        bool canManageFinance = false,
        bool canCloseSeason = false) => new()
        {
            IsAuthenticated = true,
            UserId = Guid.CreateVersion7(),
            HouseholdId = _householdId,
            Role = role,
            CanManageStock = canManageStock,
            CanManageFinance = canManageFinance,
            CanCloseSeason = canCloseSeason,
        };
}
