using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

public class SeasonNote : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Season Season { get; set; } = null!;
}
