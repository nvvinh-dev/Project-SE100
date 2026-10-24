using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Common.Auth;

// Lớp tạm coi mọi yêu cầu là chưa đăng nhập, để AppDbContext và lệnh dotnet ef dựng được trước;
// bản nạp người dùng từ database làm ở FR-AUTH-02.
public sealed class AnonymousCurrentUser : ICurrentUser
{
    public bool IsAuthenticated => false;

    // Ném lỗi thay vì trả giá trị mặc định: Guid.Empty hay UserRole mặc định (Owner) sẽ âm thầm cho qua kiểm tra quyền.
    public Guid UserId => throw new InvalidOperationException("Chưa đăng nhập, không có UserId.");

    public Guid? HouseholdId => null;

    public UserRole Role => throw new InvalidOperationException("Chưa đăng nhập, không có vai trò.");

    public bool CanManageStock => false;

    public bool CanManageFinance => false;

    public bool CanCloseSeason => false;

    public IReadOnlyList<Guid> AssignedSeasonIds => [];
}
