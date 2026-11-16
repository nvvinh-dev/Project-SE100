using System.Globalization;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>
/// Quản lý tồn nông sản và kiểm tra điều kiện tồn kho nông sản (FR-HAR-05, FR-HAR-08, BR-SEA-05, BR-SAL-01, BR-SAL-05).
/// </summary>
public class ProduceStockService(AppDbContext db, ICurrentUser currentUser) : IProduceStockService
{
    private static readonly QualityGrade[] AllGrades = [QualityGrade.Grade1, QualityGrade.Grade2, QualityGrade.Grade3];
    private static readonly CultureInfo VietnameseCulture = new("vi-VN");

    public async Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(Guid seasonId, CancellationToken ct) =>
        await GetStockAsync([seasonId], ct);

    public async Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        if (seasonIds.Count == 0)
        {
            return [];
        }

        var distinctIds = seasonIds.Distinct().ToList();

        // BR-GEN-02, BR-USR-13: chỉ Chủ hộ, thành viên trong phân công mùa vụ hoặc có ManageFinance được xem tồn mùa vụ.
        var visibleCount = await db.Seasons
            .AsNoTracking()
            .VisibleTo(currentUser, ExtraPermission.ManageFinance)
            .CountAsync(s => distinctIds.Contains(s.Id), ct);

        if (visibleCount != distinctIds.Count)
        {
            throw AppException.NotFound("mùa vụ");
        }

        return await ComputeStockAsync(distinctIds, ct);
    }

    public async Task EnsureSufficientStockForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        decimal quantity,
        Guid? currentSaleId,
        CancellationToken ct)
    {
        if (!await db.Seasons.AsNoTracking().AnyAsync(s => s.Id == seasonId, ct))
        {
            throw AppException.NotFound("mùa vụ");
        }

        // Dùng chung ComputeStockAsync để tồn nông sản chỉ tính ở một nơi (FR-HAR-05).
        var stocks = await ComputeStockAsync([seasonId], ct);
        var available = stocks.First(s => s.QualityGrade == qualityGrade).Quantity;

        if (currentSaleId is { } saleId)
        {
            // BR-SAL-03: hoàn lại sản lượng của chính giao dịch đang điều chỉnh trước khi kiểm tra.
            available += await db.Sales
                .AsNoTracking()
                .Where(s => s.Id == saleId
                    && s.SeasonId == seasonId
                    && s.QualityGrade == qualityGrade
                    && s.VoidedAt == null)
                .Select(s => (decimal?)s.Quantity)
                .SingleOrDefaultAsync(ct) ?? 0m;
        }

        // BR-SAL-01, BR-HAR-02: chỉ kiểm tra đúng cặp (mùa vụ, loại chất lượng), không bù trừ giữa các loại.
        if (quantity > available)
        {
            throw new AppException(
                ErrorCodes.InsufficientProduceStock,
                $"Tồn nông sản {GetGradeLabel(qualityGrade)} không đủ để bán (còn {FormatQuantity(available)}, cần {FormatQuantity(quantity)}).",
                new
                {
                    seasonId,
                    qualityGrade,
                    available,
                    requested = quantity,
                });
        }
    }

    public async Task EnsureZeroStockAsync(Guid seasonId, CancellationToken ct)
    {
        var crop = await db.Seasons
            .AsNoTracking()
            .Where(s => s.Id == seasonId)
            .Select(s => new { s.Crop.YieldUnit })
            .FirstOrDefaultAsync(ct)
            ?? throw AppException.NotFound("mùa vụ");

        var stocks = await ComputeStockAsync([seasonId], ct);
        // BR-SEA-05: Chặn khi còn tồn khác 0 (kể cả âm bất thường)
        var remaining = stocks.Where(s => s.Quantity != 0).ToList();
        if (remaining.Count > 0)
        {
            var unitLabel = GetUnitLabel(crop.YieldUnit);
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
                $"Mùa vụ vẫn còn tồn nông sản chưa bán hoặc điều chỉnh hết ({detailMsg}).",
                new
                {
                    seasonId,
                    remainingStock = remaining.Select(r => new
                    {
                        qualityGrade = r.QualityGrade,
                        quantity = r.Quantity,
                    }).ToList(),
                });
        }
    }

    public async Task ValidateHarvestLinksForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        IReadOnlyCollection<Guid> harvestRecordIds,
        CancellationToken ct)
    {
        var distinctIds = harvestRecordIds.Distinct().ToList();
        if (distinctIds.Count == 0)
        {
            return;
        }

        // BR-SAL-05: phiếu thu hoạch liên kết phải cùng hộ (filter tự động), cùng mùa vụ, cùng loại chất lượng và chưa Void.
        var validIds = await db.HarvestRecords
            .AsNoTracking()
            .Where(h => distinctIds.Contains(h.Id)
                && h.SeasonId == seasonId
                && h.QualityGrade == qualityGrade
                && h.VoidedAt == null)
            .Select(h => h.Id)
            .ToListAsync(ct);

        var invalidIds = distinctIds.Except(validIds).ToList();
        if (invalidIds.Count > 0)
        {
            throw new AppException(
                ErrorCodes.ValidationError,
                "Một hoặc nhiều phiếu thu hoạch liên kết không hợp lệ, đã hủy hoặc không khớp mùa vụ và loại chất lượng.",
                new { invalidHarvestRecordIds = invalidIds });
        }
    }

    private async Task<IReadOnlyList<ProduceStockResponse>> ComputeStockAsync(
        IReadOnlyList<Guid> distinctIds,
        CancellationToken ct)
    {
        var harvests = await db.HarvestRecords
            .AsNoTracking()
            .Where(h => distinctIds.Contains(h.SeasonId) && h.VoidedAt == null)
            .GroupBy(h => new { h.SeasonId, h.QualityGrade })
            .Select(g => new { g.Key.SeasonId, g.Key.QualityGrade, Total = g.Sum(h => h.Quantity) })
            .ToDictionaryAsync(x => (x.SeasonId, x.QualityGrade), x => x.Total, ct);

        var sales = await db.Sales
            .AsNoTracking()
            .Where(s => distinctIds.Contains(s.SeasonId) && s.VoidedAt == null)
            .GroupBy(s => new { s.SeasonId, s.QualityGrade })
            .Select(g => new { g.Key.SeasonId, g.Key.QualityGrade, Total = g.Sum(s => s.Quantity) })
            .ToDictionaryAsync(x => (x.SeasonId, x.QualityGrade), x => x.Total, ct);

        var adjustments = await db.ProduceStockAdjustments
            .AsNoTracking()
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
