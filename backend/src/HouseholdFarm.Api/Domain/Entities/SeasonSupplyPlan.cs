using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ là kế hoạch, không giữ chỗ tồn kho.
public class SeasonSupplyPlan : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    public Guid SupplyId { get; set; }

    // Theo đơn vị của vật tư.
    public decimal PlannedQuantity { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Season Season { get; set; } = null!;

    public Supply Supply { get; set; } = null!;
}
