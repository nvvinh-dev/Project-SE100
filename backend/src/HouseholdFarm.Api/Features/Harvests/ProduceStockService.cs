using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>
/// Quản lý tồn nông sản và kiểm tra điều kiện tồn kho nông sản (FR-HAR-05, BR-SEA-05, BR-SAL-01).
/// </summary>
public class ProduceStockService(AppDbContext? db = null) : IProduceStockService
{
    private static readonly QualityGrade[] AllGrades = [QualityGrade.Grade1, QualityGrade.Grade2, QualityGrade.Grade3];

    public async Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(Guid seasonId, CancellationToken ct)
    {
        if (db is null)
        {
            return AllGrades.Select(g => new ProduceStockResponse(seasonId, g, 0m, 0m, 0m, 0m)).ToList();
        }

        var harvests = await db.HarvestRecords
            .Where(h => h.SeasonId == seasonId && h.VoidedAt == null)
            .GroupBy(h => h.QualityGrade)
            .Select(g => new { Grade = g.Key, Total = g.Sum(h => h.Quantity) })
            .ToDictionaryAsync(x => x.Grade, x => x.Total, ct);

        var sales = await db.Sales
            .Where(s => s.SeasonId == seasonId && s.VoidedAt == null)
            .GroupBy(s => s.QualityGrade)
            .Select(g => new { Grade = g.Key, Total = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => x.Grade, x => x.Total, ct);

        var adjustments = await db.ProduceStockAdjustments
            .Where(a => a.SeasonId == seasonId && a.VoidedAt == null)
            .GroupBy(a => a.QualityGrade)
            .Select(g => new { Grade = g.Key, Total = g.Sum(a => a.Quantity) })
            .ToDictionaryAsync(x => x.Grade, x => x.Total, ct);

        return AllGrades.Select(g =>
        {
            harvests.TryGetValue(g, out var h);
            sales.TryGetValue(g, out var s);
            adjustments.TryGetValue(g, out var a);
            var stock = h - s - a;
            return new ProduceStockResponse(seasonId, g, h, s, a, stock);
        }).ToList();
    }

    public async Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct)
    {
        if (seasonIds.Count == 0) return [];

        var list = new List<ProduceStockResponse>();
        foreach (var sid in seasonIds.Distinct())
        {
            var stocks = await GetStockAsync(sid, ct);
            list.AddRange(stocks);
        }
        return list;
    }

    public Task EnsureSufficientStockForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        decimal quantity,
        Guid? currentSaleId,
        CancellationToken ct) =>
        throw new NotImplementedException();

    public async Task EnsureZeroStockAsync(Guid seasonId, CancellationToken ct)
    {
        var stocks = await GetStockAsync(seasonId, ct);
        var remaining = stocks.Where(s => s.Quantity > 0).ToList();
        if (remaining.Count > 0)
        {
            var detailMsg = string.Join(", ", remaining.Select(r => $"{r.QualityGrade}: {r.Quantity}"));
            throw new AppException(
                ErrorCodes.SeasonProduceRemaining,
                $"Mùa vụ vẫn còn tồn nông sản chưa bán hoặc điều chỉnh hết ({detailMsg}).");
        }
    }

    public Task ValidateHarvestLinksForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        IReadOnlyCollection<Guid> harvestRecordIds,
        CancellationToken ct) =>
        throw new NotImplementedException();
}
