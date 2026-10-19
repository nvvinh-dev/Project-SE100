using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ ghi thêm. Một mùa vụ có thể có nhiều bản tóm tắt khi tạo lại; giao diện hiển thị bản mới nhất.
public class AISeasonSummary : IHouseholdOwned, IAuditable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    public string Content { get; set; } = string.Empty;

    // Tên mô hình AI đã dùng.
    public string Model { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public Season Season { get; set; } = null!;
}
