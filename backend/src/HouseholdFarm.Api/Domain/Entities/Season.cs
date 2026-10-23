using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class Season : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid PlotId { get; set; }

    public Guid CropId { get; set; }

    public DateOnly StartDate { get; set; }

    public DateOnly ExpectedHarvestDate { get; set; }

    // Theo đơn vị sản lượng của cây.
    public decimal TargetYield { get; set; }

    public SeasonStatus Status { get; set; }

    public DateTime? StartedAt { get; set; }

    // Reopen xóa hai cột này; lý do mở lại lưu ở nhật ký Season.Reopen.
    public DateTime? ClosedAt { get; set; }

    public Guid? ClosedById { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Guid? CancelledById { get; set; }

    public string? CancelReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Plot Plot { get; set; } = null!;

    public Crop Crop { get; set; } = null!;

    public List<SeasonAssignment> Assignments { get; set; } = [];

    public List<SeasonSupplyPlan> SupplyPlans { get; set; } = [];

    public List<SeasonNote> Notes { get; set; } = [];

    public List<FarmTask> FarmTasks { get; set; } = [];
}
