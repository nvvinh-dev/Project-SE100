namespace HouseholdFarm.Api.Domain.Common;

// Người tạo và thời điểm tạo. Hệ thống tự điền khi lưu, service không gán tay.
public interface IAuditable
{
    DateTime CreatedAt { get; set; }

    Guid CreatedById { get; set; }
}
