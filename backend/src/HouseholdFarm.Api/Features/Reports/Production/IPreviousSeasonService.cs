using HouseholdFarm.Api.Domain.Entities;

namespace HouseholdFarm.Api.Features.Reports.Production;

/// <summary>
/// Tìm mùa vụ trước dùng chung cho báo cáo sản xuất (FR-RPT-PROD-04) và tóm tắt mùa vụ bằng AI (FR-AI-01)
/// theo quy tắc BR-RPT-11.
/// </summary>
public interface IPreviousSeasonService
{
    Task<PreviousSeasonInfo?> FindPreviousSeasonAsync(
        Guid seasonId,
        PreviousSeasonCompareBy compareBy = PreviousSeasonCompareBy.SamePlot,
        CancellationToken ct = default);

    Task<IReadOnlyDictionary<Guid, PreviousSeasonInfo?>> FindPreviousSeasonsAsync(
        IReadOnlyCollection<Season> currentSeasons,
        PreviousSeasonCompareBy compareBy,
        CancellationToken ct = default);
}
