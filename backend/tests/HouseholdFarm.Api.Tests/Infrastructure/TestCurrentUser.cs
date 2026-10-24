using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Tests.Infrastructure;

// Người dùng hiện tại dựng sẵn cho test không đi qua đăng nhập.
public sealed class TestCurrentUser : ICurrentUser
{
    public static TestCurrentUser Anonymous { get; } = new();

    public bool IsAuthenticated { get; init; }

    public Guid UserId { get; init; }

    public Guid? HouseholdId { get; init; }

    public UserRole Role { get; init; }

    public bool CanManageStock { get; init; }

    public bool CanManageFinance { get; init; }

    public bool CanCloseSeason { get; init; }

    public IReadOnlyList<Guid> AssignedSeasonIds { get; init; } = [];

    public static TestCurrentUser Owner(Guid householdId) => new()
    {
        IsAuthenticated = true,
        UserId = Guid.CreateVersion7(),
        HouseholdId = householdId,
        Role = UserRole.Owner,
    };

    public static TestCurrentUser SystemAdmin() => new()
    {
        IsAuthenticated = true,
        UserId = Guid.CreateVersion7(),
        Role = UserRole.SystemAdmin,
    };
}
