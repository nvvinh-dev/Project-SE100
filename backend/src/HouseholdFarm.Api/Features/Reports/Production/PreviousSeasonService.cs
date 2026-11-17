using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Reports.Production;

public sealed class PreviousSeasonService(AppDbContext db, ICurrentUser currentUser) : IPreviousSeasonService
{
    public async Task<PreviousSeasonInfo?> FindPreviousSeasonAsync(
        Guid seasonId,
        PreviousSeasonCompareBy compareBy = PreviousSeasonCompareBy.SamePlot,
        bool includeFinanceScope = false,
        CancellationToken ct = default)
    {
        var season = await db.Seasons.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == seasonId, ct);
        if (season is null)
        {
            return null;
        }

        var map = await FindPreviousSeasonsAsync([season], compareBy, includeFinanceScope, ct);
        return map.GetValueOrDefault(seasonId);
    }

    public async Task<IReadOnlyDictionary<Guid, PreviousSeasonInfo?>> FindPreviousSeasonsAsync(
        IReadOnlyCollection<Season> currentSeasons,
        PreviousSeasonCompareBy compareBy,
        bool includeFinanceScope = false,
        CancellationToken ct = default)
    {
        var result = new Dictionary<Guid, PreviousSeasonInfo?>();
        if (currentSeasons.Count == 0)
        {
            return result;
        }

        var maxStartDate = currentSeasons.Max(s => s.StartDate);
        var keyIds = compareBy == PreviousSeasonCompareBy.SamePlot
            ? currentSeasons.Select(s => s.PlotId).Distinct().ToList()
            : currentSeasons.Select(s => s.CropId).Distinct().ToList();

        // BR-RPT-11 & FR-RPT-02: chỉ xét mùa vụ Closed trong phạm vi người xem, bỏ qua Cancelled và mùa chưa chốt.
        var scopedQuery = includeFinanceScope
            ? db.Seasons.AsNoTracking().VisibleTo(currentUser, ExtraPermission.ManageFinance)
            : db.Seasons.AsNoTracking().VisibleTo(currentUser);

        var candidatesQuery = scopedQuery
            .Where(s => s.Status == SeasonStatus.Closed && s.StartDate < maxStartDate);

        candidatesQuery = compareBy == PreviousSeasonCompareBy.SamePlot
            ? candidatesQuery.Where(s => keyIds.Contains(s.PlotId))
            : candidatesQuery.Where(s => keyIds.Contains(s.CropId));

        var candidates = await candidatesQuery
            .Select(s => new CandidateSeason(
                s.Id,
                s.PlotId,
                s.Plot.Code,
                s.CropId,
                s.Crop.Name,
                s.Crop.YieldUnit,
                s.Plot.AreaM2,
                s.StartDate,
                s.ClosedAt,
                s.CreatedAt))
            .ToListAsync(ct);

        var matchedByCurrentId = new Dictionary<Guid, CandidateSeason?>();
        var matchedIds = new HashSet<Guid>();

        foreach (var current in currentSeasons)
        {
            var matched = candidates
                .Where(c =>
                    c.Id != current.Id
                    && c.StartDate < current.StartDate
                    && (compareBy == PreviousSeasonCompareBy.SamePlot
                        ? c.PlotId == current.PlotId
                        : c.CropId == current.CropId))
                .OrderByDescending(c => c.StartDate)
                .ThenByDescending(c => c.ClosedAt)
                .ThenByDescending(c => c.CreatedAt)
                .FirstOrDefault();

            matchedByCurrentId[current.Id] = matched;
            if (matched is not null)
            {
                matchedIds.Add(matched.Id);
            }
        }

        var yieldBySeasonId = matchedIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await db.HarvestRecords.AsNoTracking()
                .Where(h => matchedIds.Contains(h.SeasonId) && h.VoidedAt == null)
                .GroupBy(h => h.SeasonId)
                .Select(g => new { SeasonId = g.Key, Total = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.SeasonId, x => x.Total, ct);

        foreach (var current in currentSeasons)
        {
            var matched = matchedByCurrentId[current.Id];
            if (matched is null)
            {
                result[current.Id] = null;
                continue;
            }

            var actualYield = yieldBySeasonId.GetValueOrDefault(matched.Id, 0m);
            var yieldPerM2 = matched.AreaM2 > 0m
                ? Math.Round(actualYield / matched.AreaM2, 6, MidpointRounding.AwayFromZero)
                : 0m;
            var yieldPerHa = matched.AreaM2 > 0m
                ? Math.Round(actualYield * 10_000m / matched.AreaM2, 3, MidpointRounding.AwayFromZero)
                : 0m;

            result[current.Id] = new PreviousSeasonInfo(
                matched.Id,
                matched.PlotId,
                matched.PlotCode,
                matched.CropId,
                matched.CropName,
                matched.YieldUnit,
                matched.AreaM2,
                matched.StartDate,
                actualYield,
                yieldPerM2,
                yieldPerHa);
        }

        return result;
    }

    private sealed record CandidateSeason(
        Guid Id,
        Guid PlotId,
        string PlotCode,
        Guid CropId,
        string CropName,
        Unit YieldUnit,
        decimal AreaM2,
        DateOnly StartDate,
        DateTime? ClosedAt,
        DateTime CreatedAt);
}
