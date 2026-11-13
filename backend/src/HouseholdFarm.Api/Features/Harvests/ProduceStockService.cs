using System.Globalization;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>
/// Quản lý tồn nông sản và kiểm tra điều kiện tồn kho nông sản (FR-HAR-05, BR-SEA-05, BR-SAL-01).
/// </summary>
public class ProduceStockService(AppDbContext db) : IProduceStockService
{
    private static readonly QualityGrade[] AllGrades = [QualityGrade.Grade1, QualityGrade.Grade2, QualityGrade.Grade3];
    private static readonly CultureInfo VietnameseCulture = new("vi-VN");

    public async Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(Guid seasonId, CancellationToken ct)
    {
        return await GetStockAsync([seasonId], ct);
    }

    public async Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct)
    {
        if (seasonIds.Count == 0) return [];

        var distinctIds = seasonIds.Distinct().ToList();

        var harvests = await db.HarvestRecords
            .Where(h => distinctIds.Contains(h.SeasonId) && h.VoidedAt == null)
            .GroupBy(h => new { h.SeasonId, h.QualityGrade })
            .Select(g => new { g.Key.SeasonId, g.Key.QualityGrade, Total = g.Sum(h => h.Quantity) })
            .ToDictionaryAsync(x => (x.SeasonId, x.QualityGrade), x => x.Total, ct);

        var sales = await db.Sales
            .Where(s => distinctIds.Contains(s.SeasonId) && s.VoidedAt == null)
            .GroupBy(s => new { s.SeasonId, s.QualityGrade })
            .Select(g => new { g.Key.SeasonId, g.Key.QualityGrade, Total = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => (x.SeasonId, x.QualityGrade), x => x.Total, ct);

        var adjustments = await db.ProduceStockAdjustments
            .Where(a => distinctIds.Contains(a.SeasonId) && a.VoidedAt == null)
            .GroupBy(a => new { a.SeasonId, a.QualityGrade })
            .Select(g => new { g.Key.SeasonId, g.Key.QualityGrade, Total = g.Sum(a => a.Quantity) })
            .ToDictionaryAsync(x => (x.SeasonId, x.QualityGrade), x => x.Total, ct);

        var result = new List<ProduceStockResponse>(distinctIds.Count * AllGrades.Length);
        foreach (var sid in distinctIds)
        {
            foreach (var g in AllGrades)
            {
                harvests.TryGetValue((sid, g), out var h);
                sales.TryGetValue((sid, g), out var s);
                adjustments.TryGetValue((sid, g), out var a);
                var stock = h - s - a;
                result.Add(new ProduceStockResponse(sid, g, h, s, a, stock));
            }
        }

        return result;
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
        // BR-SEA-05: Chặn khi còn tồn khác 0 (kể cả âm bất thường)
        var remaining = stocks.Where(s => s.Quantity != 0).ToList();
        if (remaining.Count > 0)
        {
            var crop = await db.Seasons
                .AsNoTracking()
                .Where(s => s.Id == seasonId)
                .Select(s => new { s.Crop.YieldUnit })
                .FirstOrDefaultAsync(ct);

            var unitLabel = crop != null ? GetUnitLabel(crop.YieldUnit) : "";
            var detailMsg = string.Join(", ", remaining.Select(r =>
            {
                var gradeLabel = GetGradeLabel(r.QualityGrade);
                var formattedQty = FormatQuantity(r.Quantity);
                return string.IsNullOrEmpty(unitLabel)
                    ? $"{gradeLabel}: {formattedQty}"
                    : $"{gradeLabel}: {formattedQty} {unitLabel}";
            }));

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

    private static string FormatQuantity(decimal value) => value.ToString("#,##0.###", VietnameseCulture);

    private static string GetGradeLabel(QualityGrade grade) => grade switch
    {
        QualityGrade.Grade1 => "Loại 1",
        QualityGrade.Grade2 => "Loại 2",
        QualityGrade.Grade3 => "Loại 3",
        _ => grade.ToString(),
    };

    private static string GetUnitLabel(Unit unit) => unit switch
    {
        Unit.Kilogram => "kg",
        Unit.Gram => "g",
        Unit.Quintal => "tạ",
        Unit.Ton => "tấn",
        Unit.Liter => "lít",
        Unit.Milliliter => "ml",
        Unit.Bag => "bao",
        Unit.Bottle => "chai",
        Unit.Pack => "gói",
        Unit.Plant => "cây",
        Unit.Fruit => "quả",
        Unit.Bunch => "bó",
        _ => unit.ToString(),
    };
}
