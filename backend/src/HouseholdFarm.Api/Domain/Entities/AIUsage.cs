using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ ghi thêm, dùng để đếm giới hạn lượt gọi AI theo hộ, theo ngày (BR-AI-04).
public class AIUsage : IHouseholdOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid UserId { get; set; }

    public AIFeature Feature { get; set; }

    // Ngày theo giờ Việt Nam, không theo UTC, để lượt được làm mới đúng lúc nửa đêm ở Việt Nam.
    public DateOnly UsageDate { get; set; }

    public DateTime CreatedAt { get; set; }

    public ApplicationUser User { get; set; } = null!;
}
