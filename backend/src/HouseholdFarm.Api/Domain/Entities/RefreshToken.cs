namespace HouseholdFarm.Api.Domain.Entities;

// Không có HouseholdId: token gắn với tài khoản, không áp dụng Global Query Filter.
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid UserId { get; set; }

    // Chỉ lưu giá trị băm để lộ database cũng không dùng lại được token.
    public string TokenHash { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? RevokedAt { get; set; }

    // BR-USR-05: token mới thay thế khi làm mới phiên.
    public Guid? ReplacedById { get; set; }

    public ApplicationUser User { get; set; } = null!;

    public RefreshToken? ReplacedBy { get; set; }
}
