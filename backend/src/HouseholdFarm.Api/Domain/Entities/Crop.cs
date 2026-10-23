using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class Crop : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public CropGroup Group { get; set; }

    public string? GroupOther { get; set; }

    // BR-CAT-05: không đổi được khi cây đã có phiếu thu hoạch.
    public Unit YieldUnit { get; set; }

    public string? ReferenceInfo { get; set; }

    public RecordStatus Status { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }
}
