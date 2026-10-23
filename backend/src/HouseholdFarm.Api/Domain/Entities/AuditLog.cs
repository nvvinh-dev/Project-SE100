namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ ghi thêm, không sửa, không xóa (FR-AUD-04). Không có các cột chung; IAuditLogger tự điền OccurredAt, ActorId.
public class AuditLog
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    // Để trống với thao tác không thuộc hộ nào.
    public Guid? HouseholdId { get; set; }

    public Guid? ActorId { get; set; }

    // Dạng <Entity>.<Thao tác>, ví dụ Season.Close.
    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }

    // JSON chỉ gồm các trường bị đổi, không phải bản sao cả bản ghi (BR-GEN-08).
    public string? OldValues { get; set; }

    public string? NewValues { get; set; }

    public string? Reason { get; set; }

    public DateTime OccurredAt { get; set; }
}
