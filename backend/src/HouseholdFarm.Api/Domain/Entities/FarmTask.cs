using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class FarmTask : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    // Để trống nghĩa là chưa giao người.
    public Guid? AssigneeId { get; set; }

    public FarmTaskType Type { get; set; }

    public string? TypeOther { get; set; }

    public DateOnly ScheduledDate { get; set; }

    public int? PlannedDurationMinutes { get; set; }

    public string? Note { get; set; }

    public FarmTaskStatus Status { get; set; }

    public DateOnly? PerformedDate { get; set; }

    public int? ActualDurationMinutes { get; set; }

    public string? AbnormalNote { get; set; }

    public string? FieldNote { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    // Không phải khóa ngoại: chỉ để nhận ra các công việc sinh từ cùng một lần tạo lặp (BR-TSK-04).
    public Guid? RecurrenceGroupId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Season Season { get; set; } = null!;

    public ApplicationUser? Assignee { get; set; }

    public List<FarmTaskSupplyUsage> SupplyUsages { get; set; } = [];
}
