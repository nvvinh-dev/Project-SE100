namespace HouseholdFarm.Api.Features.Seasons;

public interface ISeasonService
{
    Task<SeasonResponse> CreateAsync(CreateSeasonRequest request, CancellationToken ct);

    Task<SeasonResponse> GetAsync(Guid id, CancellationToken ct);
}

