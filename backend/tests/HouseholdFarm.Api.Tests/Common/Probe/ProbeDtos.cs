using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Tests.Common.Probe;

public sealed record ProbeResponse(
    Guid Id,
    string Name,
    decimal AreaM2,
    SoilType SoilType,
    DateOnly StartDate,
    DateTime CreatedAt,
    uint Version);

public sealed record ProbeLine(decimal Quantity);

public sealed record CreateProbeRequest(
    string Name,
    decimal AreaM2,
    SoilType SoilType,
    DateOnly StartDate,
    IReadOnlyList<ProbeLine> Lines,
    string? Note);

public sealed record ProbeQuery : PageQuery;

public sealed record ProbeListItem(int Number);
