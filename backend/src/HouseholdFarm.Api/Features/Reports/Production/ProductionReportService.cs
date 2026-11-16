using System.Globalization;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;
using HouseholdFarm.Api.Features.Reports.Finance;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Reports.Production;

public sealed class ProductionReportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IConfiguration configuration,
    IReportAccessService access,
    IReportExportService exportService,
    IPreviousSeasonService previousSeasonService,
    ISeasonFinanceService seasonFinanceService) : IProductionReportService
{
    private const decimal DefaultDeviationThreshold = 20m;

    private readonly TimeZoneInfo _timeZone = ReportSeasonQueryExtensions.GetAppTimeZone(configuration);

    public async Task<ProductionReportResponse> GetAsync(ProductionReportQuery query, CancellationToken ct)
    {
        access.EnsureCanViewReport(ReportType.Production);

        var threshold = query.DeviationThreshold ?? DefaultDeviationThreshold;
        var includesFinancialColumns = access.CanViewFinancialColumns();

        var seasons = await db.Seasons.AsNoTracking()
            .VisibleToReport(currentUser, ReportType.Production)
            .ApplyReportFilter(query, _timeZone)
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .OrderByDescending(s => s.StartDate)
            .ThenBy(s => s.Crop.Name)
            .ThenBy(s => s.Plot.Code)
            .ToListAsync(ct);

        // BR-RPT-12: tổng quan tính trên toàn bộ mùa vụ khớp bộ lọc (chưa cắt theo OnlyHighDeviation).
        var overview = new ProductionOverviewDto(
            InProgressCount: seasons.Count(s => s.Status == SeasonStatus.InProgress),
            ClosedCount: seasons.Count(s => s.Status == SeasonStatus.Closed),
            PlannedCount: seasons.Count(s => s.Status == SeasonStatus.Planned),
            CancelledCount: seasons.Count(s => s.Status == SeasonStatus.Cancelled),
            TotalCultivatedAreaM2: seasons
                .Where(s => s.Status is SeasonStatus.InProgress or SeasonStatus.Closed)
                .Sum(s => s.Plot.AreaM2));

        var (activeSeasons, cancelledSeasons) = seasons.PartitionCancelled(s => s.Status);

        var allSeasonIds = seasons.Select(s => s.Id).ToList();
        var yieldBySeasonId = allSeasonIds.Count == 0
            ? new Dictionary<Guid, decimal>()
            : await db.HarvestRecords.AsNoTracking()
                .Where(h => allSeasonIds.Contains(h.SeasonId) && h.VoidedAt == null)
                .GroupBy(h => h.SeasonId)
                .Select(g => new { SeasonId = g.Key, Total = g.Sum(x => x.Quantity) })
                .ToDictionaryAsync(x => x.SeasonId, x => x.Total, ct);

        var previousBySeasonId = await previousSeasonService.FindPreviousSeasonsAsync(activeSeasons, query.CompareBy, ct);

        // BR-RPT-04: chỉ gọi dữ liệu tài chính và trả cột tài chính khi người dùng có quyền Quản lý tài chính.
        var financeBySeasonId = new Dictionary<Guid, SeasonFinanceResponse>();
        if (includesFinancialColumns && allSeasonIds.Count > 0)
        {
            var financeIds = allSeasonIds
                .Concat(previousBySeasonId.Values.Where(p => p is not null).Select(p => p!.SeasonId))
                .Distinct()
                .ToList();
            var financeRows = await seasonFinanceService.GetAsync(financeIds, ct);
            financeBySeasonId = financeRows.ToDictionary(x => x.SeasonId);
        }

        var activeItems = activeSeasons
            .Select(season => BuildItem(
                season,
                yieldBySeasonId.GetValueOrDefault(season.Id, 0m),
                threshold,
                includesFinancialColumns,
                financeBySeasonId.GetValueOrDefault(season.Id),
                previousBySeasonId.GetValueOrDefault(season.Id),
                previousBySeasonId.GetValueOrDefault(season.Id) is { } prev
                    ? financeBySeasonId.GetValueOrDefault(prev.SeasonId)
                    : null))
            .Where(item => !query.OnlyHighDeviation || item.IsHighDeviation)
            .ToList();

        var cancelledItems = cancelledSeasons
            .Select(season => BuildItem(
                season,
                yieldBySeasonId.GetValueOrDefault(season.Id, 0m),
                threshold,
                includesFinancialColumns,
                financeBySeasonId.GetValueOrDefault(season.Id),
                previous: null,
                previousFinance: null,
                includeComparison: false))
            .ToList();

        return new ProductionReportResponse(
            overview,
            threshold,
            query.CompareBy,
            includesFinancialColumns,
            activeItems,
            cancelledItems);
    }

    public async Task<ExportedFileResult> ExportAsync(ExportProductionReportRequest request, CancellationToken ct)
    {
        var query = new ProductionReportQuery
        {
            From = request.From,
            To = request.To,
            SeasonId = request.SeasonId,
            PlotId = request.PlotId,
            CropId = request.CropId,
            DeviationThreshold = request.DeviationThreshold ?? DefaultDeviationThreshold,
            OnlyHighDeviation = request.OnlyHighDeviation,
            CompareBy = request.CompareBy,
        };

        var report = await GetAsync(query, ct);
        var document = await BuildDocumentAsync(query, report, ct);

        return await exportService.ExportAsync(ReportType.Production, request.Format!.Value, query, request, document, ct);
    }

    private static ProductionSeasonItemDto BuildItem(
        Season season,
        decimal actualYield,
        decimal threshold,
        bool includesFinance,
        SeasonFinanceResponse? finance,
        PreviousSeasonInfo? previous,
        SeasonFinanceResponse? previousFinance,
        bool includeComparison = true)
    {
        var yieldDeviation = season.TargetYield > 0m
            ? Math.Round((actualYield - season.TargetYield) / season.TargetYield * 100m, 1, MidpointRounding.AwayFromZero)
            : 0m;
        // BR-RPT-10: chỉ đánh dấu "lệch nhiều" khi mùa vụ đã Closed và |YieldDeviation| >= ngưỡng.
        var isHighDeviation = season.Status == SeasonStatus.Closed && Math.Abs(yieldDeviation) >= threshold;

        var areaM2 = season.Plot.AreaM2;
        var yieldPerM2 = areaM2 > 0m
            ? Math.Round(actualYield / areaM2, 6, MidpointRounding.AwayFromZero)
            : 0m;
        var yieldPerHa = areaM2 > 0m
            ? Math.Round(actualYield * 10_000m / areaM2, 3, MidpointRounding.AwayFromZero)
            : 0m;

        ProductionSeasonComparisonDto? comparison = null;
        if (includeComparison)
        {
            comparison = BuildComparison(
                season.Crop.YieldUnit,
                yieldPerHa,
                areaM2,
                includesFinance,
                finance,
                previous,
                previousFinance);
        }

        return new ProductionSeasonItemDto(
            season.ToSummaryDto(),
            actualYield,
            season.TargetYield,
            yieldDeviation,
            isHighDeviation,
            yieldPerM2,
            yieldPerHa,
            includesFinance ? finance?.SupplyCost ?? 0m : null,
            includesFinance ? finance?.Revenue ?? 0m : null,
            includesFinance ? finance?.Profit ?? 0m : null,
            comparison);
    }

    private static ProductionSeasonComparisonDto BuildComparison(
        Unit currentUnit,
        decimal currentYieldPerHa,
        decimal currentAreaM2,
        bool includesFinance,
        SeasonFinanceResponse? currentFinance,
        PreviousSeasonInfo? previous,
        SeasonFinanceResponse? previousFinance)
    {
        if (previous is null)
        {
            return new ProductionSeasonComparisonDto(
                null,
                null,
                null,
                null,
                null,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                "Chưa có mùa trước để so sánh");
        }

        var sameUnit = currentUnit == previous.YieldUnit;
        decimal? yieldPerHaDelta = sameUnit
            ? Math.Round(currentYieldPerHa - previous.YieldPerHectare, 3, MidpointRounding.AwayFromZero)
            : null;
        var message = sameUnit ? null : "Khác đơn vị, không so sánh";

        decimal? prevSupplyCost = null;
        decimal? supplyCostDelta = null;
        decimal? currSupplyPerM2 = null;
        decimal? prevSupplyPerM2 = null;
        decimal? supplyPerM2Delta = null;
        decimal? prevRevenue = null;
        decimal? revenueDelta = null;
        decimal? currRevenuePerM2 = null;
        decimal? prevRevenuePerM2 = null;
        decimal? revenuePerM2Delta = null;

        if (includesFinance)
        {
            var currSupply = currentFinance?.SupplyCost ?? 0m;
            var prevSupply = previousFinance?.SupplyCost ?? 0m;
            var currRev = currentFinance?.Revenue ?? 0m;
            var prevRev = previousFinance?.Revenue ?? 0m;

            prevSupplyCost = prevSupply;
            supplyCostDelta = currSupply - prevSupply;
            currSupplyPerM2 = PerM2(currSupply, currentAreaM2);
            prevSupplyPerM2 = PerM2(prevSupply, previous.AreaM2);
            supplyPerM2Delta = currSupplyPerM2.HasValue && prevSupplyPerM2.HasValue
                ? currSupplyPerM2.Value - prevSupplyPerM2.Value
                : null;

            prevRevenue = prevRev;
            revenueDelta = currRev - prevRev;
            currRevenuePerM2 = PerM2(currRev, currentAreaM2);
            prevRevenuePerM2 = PerM2(prevRev, previous.AreaM2);
            revenuePerM2Delta = currRevenuePerM2.HasValue && prevRevenuePerM2.HasValue
                ? currRevenuePerM2.Value - prevRevenuePerM2.Value
                : null;
        }

        return new ProductionSeasonComparisonDto(
            previous.SeasonId,
            $"{previous.CropName} · {previous.PlotCode} ({previous.StartDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})",
            previous.StartDate,
            previous.YieldUnit,
            previous.YieldPerHectare,
            yieldPerHaDelta,
            sameUnit,
            prevSupplyCost,
            supplyCostDelta,
            currSupplyPerM2,
            prevSupplyPerM2,
            supplyPerM2Delta,
            prevRevenue,
            revenueDelta,
            currRevenuePerM2,
            prevRevenuePerM2,
            revenuePerM2Delta,
            message);
    }

    private static decimal? PerM2(decimal amount, decimal areaM2) =>
        areaM2 > 0m ? Math.Round(amount / areaM2, 2, MidpointRounding.AwayFromZero) : null;

    private async Task<ReportExportDocument> BuildDocumentAsync(
        ProductionReportQuery query,
        ProductionReportResponse report,
        CancellationToken ct)
    {
        var filters = new List<ReportSummaryItem>
        {
            new("Từ ngày", query.From?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Đến ngày", query.To?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Ngưỡng lệch (%)", report.DeviationThreshold.ToString("0.#", CultureInfo.InvariantCulture)),
            new("Chỉ mùa vụ lệch nhiều", query.OnlyHighDeviation ? "Có" : "Không"),
            new("So sánh mùa trước theo", query.CompareBy == PreviousSeasonCompareBy.SameCrop ? "Cùng cây" : "Cùng thửa"),
        };

        if (query.SeasonId is { } seasonId)
        {
            var label = await db.Seasons.AsNoTracking()
                .Where(s => s.Id == seasonId)
                .Select(s => s.Crop.Name + " · " + s.Plot.Code)
                .FirstOrDefaultAsync(ct);
            filters.Add(new("Mùa vụ", label ?? "Không tìm thấy"));
        }

        if (query.PlotId is { } plotId)
        {
            var code = await db.Plots.AsNoTracking().Where(p => p.Id == plotId).Select(p => p.Code).FirstOrDefaultAsync(ct);
            filters.Add(new("Thửa đất", code ?? "Không tìm thấy"));
        }

        if (query.CropId is { } cropId)
        {
            var name = await db.Crops.AsNoTracking().Where(c => c.Id == cropId).Select(c => c.Name).FirstOrDefaultAsync(ct);
            filters.Add(new("Cây trồng", name ?? "Không tìm thấy"));
        }

        var vi = CultureInfo.GetCultureInfo("vi-VN");
        var totalHa = report.Overview.TotalCultivatedAreaM2 / 10_000m;
        var overview = new List<ReportSummaryItem>
        {
            new("Đang canh tác", report.Overview.InProgressCount.ToString(CultureInfo.InvariantCulture)),
            new("Đã chốt", report.Overview.ClosedCount.ToString(CultureInfo.InvariantCulture)),
            new("Lên kế hoạch", report.Overview.PlannedCount.ToString(CultureInfo.InvariantCulture)),
            new("Đã hủy", report.Overview.CancelledCount.ToString(CultureInfo.InvariantCulture)),
            new(
                "Tổng diện tích gieo trồng",
                $"{report.Overview.TotalCultivatedAreaM2.ToString("#,##0.##", vi)} m² ({totalHa.ToString("#,##0.####", vi)} ha)"),
        };

        var columns = new List<ReportColumn>
        {
            new("Cây · Thửa", ReportColumnType.Text),
            new("Trạng thái", ReportColumnType.Text),
            new("Diện tích (m²)", ReportColumnType.Decimal),
            new("Thực tế", ReportColumnType.Decimal),
            new("Mục tiêu", ReportColumnType.Decimal),
            new("Đơn vị", ReportColumnType.Text),
            new("Độ lệch (%)", ReportColumnType.Percent),
            new("Lệch nhiều", ReportColumnType.Text),
            new("Năng suất / ha", ReportColumnType.Decimal),
        };
        if (report.IncludesFinancialColumns)
        {
            columns.Add(new("Chi phí vật tư", ReportColumnType.Money));
            columns.Add(new("Doanh thu", ReportColumnType.Money));
            columns.Add(new("Lợi nhuận", ReportColumnType.Money));
        }

        IReadOnlyList<object?> ToSeasonRow(ProductionSeasonItemDto item)
        {
            var statusLabel = item.Season.IsProvisional
                ? $"{ReportLabels.Of(item.Season.Status)} (tạm tính)"
                : ReportLabels.Of(item.Season.Status);
            var row = new List<object?>
            {
                $"{item.Season.CropName} · {item.Season.PlotCode}",
                statusLabel,
                item.Season.AreaM2,
                item.ActualYield,
                item.TargetYield,
                ReportLabels.Of(item.Season.YieldUnit),
                item.YieldDeviation,
                item.IsHighDeviation ? "Lệch nhiều" : "—",
                item.YieldPerHectare,
            };
            if (report.IncludesFinancialColumns)
            {
                row.Add(item.SupplyCost);
                row.Add(item.Revenue);
                row.Add(item.Profit);
            }
            return row;
        }

        var sections = new List<ReportTableSection>
        {
            new(
                "Chi tiết sản lượng theo mùa vụ",
                columns,
                report.Seasons.Select(ToSeasonRow).ToList(),
                "Mùa vụ chưa chốt ghi nhãn (tạm tính) và không bị đánh dấu lệch nhiều."),
        };

        if (report.Seasons.Count > 0)
        {
            var comparisonColumns = new List<ReportColumn>
            {
                new("Mùa vụ", ReportColumnType.Text),
                new("Mùa trước", ReportColumnType.Text),
                new("Chênh lệch năng suất / ha", ReportColumnType.Decimal),
            };
            if (report.IncludesFinancialColumns)
            {
                comparisonColumns.Add(new("Chênh lệch chi phí VT", ReportColumnType.Money));
                comparisonColumns.Add(new("Chênh lệch chi phí VT / m²", ReportColumnType.Money));
                comparisonColumns.Add(new("Chênh lệch doanh thu", ReportColumnType.Money));
                comparisonColumns.Add(new("Chênh lệch doanh thu / m²", ReportColumnType.Money));
            }
            comparisonColumns.Add(new("Ghi chú", ReportColumnType.Text));

            var comparisonRows = report.Seasons.Select(item =>
            {
                var comp = item.PreviousComparison;
                var row = new List<object?>
                {
                    $"{item.Season.CropName} · {item.Season.PlotCode}",
                    comp?.PreviousSeasonLabel ?? "—",
                    comp?.YieldPerHectareDelta,
                };
                if (report.IncludesFinancialColumns)
                {
                    row.Add(comp?.SupplyCostDelta);
                    row.Add(comp?.SupplyCostPerM2Delta);
                    row.Add(comp?.RevenueDelta);
                    row.Add(comp?.RevenuePerM2Delta);
                }
                row.Add(comp?.Message ?? "—");
                return (IReadOnlyList<object?>)row;
            }).ToList();

            sections.Add(new("So với mùa trước", comparisonColumns, comparisonRows));
        }

        if (report.CancelledSeasons.Count > 0)
        {
            sections.Add(new(
                "Mùa vụ đã hủy",
                columns,
                report.CancelledSeasons.Select(ToSeasonRow).ToList(),
                "Mùa vụ đã hủy tách riêng, không tính vào so sánh năng suất."));
        }

        return new ReportExportDocument(
            "Báo cáo sản xuất",
            "bao-cao-san-xuat",
            filters,
            overview,
            sections,
            "Sản xuất");
    }
}
