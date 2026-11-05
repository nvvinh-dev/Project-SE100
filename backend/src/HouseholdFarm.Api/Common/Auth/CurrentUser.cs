using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Common.Auth;

/// <summary>
/// Bản thật của <see cref="ICurrentUser"/>, phạm vi yêu cầu: <see cref="CurrentUserMiddleware"/> nạp từ database
/// một lần mỗi yêu cầu (D-11). Chưa được nạp nghĩa là chưa đăng nhập.
/// </summary>
public sealed class CurrentUser : ICurrentUser
{
    private Guid? _userId;
    private UserRole? _role;

    public bool IsAuthenticated => _userId is not null;

    // Ném lỗi thay vì trả giá trị mặc định: Guid.Empty hay UserRole mặc định (Owner) sẽ âm thầm cho qua kiểm tra quyền.
    public Guid UserId => _userId ?? throw new InvalidOperationException("Chưa đăng nhập, không có UserId.");

    public Guid? HouseholdId { get; private set; }

    public UserRole Role => _role ?? throw new InvalidOperationException("Chưa đăng nhập, không có vai trò.");

    public bool CanManageStock { get; private set; }

    public bool CanManageFinance { get; private set; }

    public bool CanCloseSeason { get; private set; }

    public IReadOnlyList<Guid> AssignedSeasonIds { get; private set; } = [];

    internal void SignIn(
        Guid userId,
        Guid? householdId,
        UserRole role,
        bool canManageStock,
        bool canManageFinance,
        bool canCloseSeason)
    {
        _userId = userId;
        HouseholdId = householdId;
        _role = role;
        CanManageStock = canManageStock;
        CanManageFinance = canManageFinance;
        CanCloseSeason = canCloseSeason;
    }

    internal void SetAssignedSeasonIds(IReadOnlyList<Guid> seasonIds) => AssignedSeasonIds = seasonIds;
}
