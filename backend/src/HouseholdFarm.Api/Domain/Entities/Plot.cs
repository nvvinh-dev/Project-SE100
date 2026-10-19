using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class Plot : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public string Code { get; set; } = string.Empty;

    // Luôn tính bằng m².
    public decimal AreaM2 { get; set; }

    public string? LocationDescription { get; set; }

    public SoilType SoilType { get; set; }

    public string? SoilTypeOther { get; set; }

    public WaterSource WaterSource { get; set; }

    public string? WaterSourceOther { get; set; }

    public RecordStatus Status { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }
}
