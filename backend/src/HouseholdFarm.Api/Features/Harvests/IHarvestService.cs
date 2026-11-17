using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>Quản lý phiếu thu hoạch nông sản của mùa vụ (FR-HAR-01, FR-HAR-03, FR-HAR-04).</summary>
public interface IHarvestService
{
    Task<PagedResult<HarvestRecordListItem>> GetListAsync(HarvestRecordQuery query, CancellationToken ct);

    Task<HarvestRecordResponse> GetAsync(Guid id, CancellationToken ct);

    Task<HarvestRecordResponse> CreateAsync(CreateHarvestRecordRequest request, CancellationToken ct);

    Task<HarvestRecordResponse> CorrectAsync(Guid id, CorrectHarvestRecordRequest request, CancellationToken ct);

    Task<HarvestRecordResponse> VoidAsync(Guid id, VoidHarvestRecordRequest request, CancellationToken ct);
}
