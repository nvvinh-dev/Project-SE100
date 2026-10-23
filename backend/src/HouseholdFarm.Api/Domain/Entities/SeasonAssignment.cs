using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ ghi thêm, xóa thật khi thu hồi phân công nên không có cột sửa.
public class SeasonAssignment : IHouseholdOwned, IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public Season Season { get; set; } = null!;

    public ApplicationUser User { get; set; } = null!;
}
