using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;

namespace HouseholdFarm.Api.Features.Reports.Production;

/// <summary>Tiêu chí chọn mùa trước khi so sánh sản xuất (BR-RPT-11).</summary>
public enum PreviousSeasonCompareBy
{
    SamePlot,
    SameCrop,
}

/// <summary>Bộ lọc báo cáo sản xuất (FR-RPT-01, BR-RPT-10, BR-RPT-11).</summary>
public sealed record ProductionReportQuery : ReportFilter
{
    public decimal? DeviationThreshold { get; init; } = 20m;

    public bool OnlyHighDeviation { get; init; }

    public PreviousSeasonCompareBy CompareBy { get; init; } = PreviousSeasonCompareBy.SamePlot;
}

/// <summary>Yêu cầu xuất báo cáo sản xuất (FR-RPT-04, FR-RPT-05, FR-RPT-06).</summary>
public sealed record ExportProductionReportRequest : ExportReportRequest
{
    public decimal? DeviationThreshold { get; init; } = 20m;

    public bool OnlyHighDeviation { get; init; }

    public PreviousSeasonCompareBy CompareBy { get; init; } = PreviousSeasonCompareBy.SamePlot;
}

/// <summary>Số liệu tổng quan của báo cáo sản xuất (FR-RPT-PROD-01, BR-RPT-12).</summary>
public sealed record ProductionOverviewDto(
    int InProgressCount,
    int ClosedCount,
    int PlannedCount,
    int CancelledCount,
    decimal TotalCultivatedAreaM2);

/// <summary>Thông tin mùa trước được chọn theo BR-RPT-11, dùng chung cho báo cáo sản xuất và tóm tắt AI.</summary>
public sealed record PreviousSeasonInfo(
    Guid SeasonId,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Unit YieldUnit,
    decimal AreaM2,
    DateOnly StartDate,
    decimal ActualYield,
    decimal YieldPerM2,
    decimal YieldPerHectare);

/// <summary>Kết quả so sánh một mùa vụ với mùa trước (FR-RPT-PROD-04, BR-RPT-11, BR-RPT-04).</summary>
public sealed record ProductionSeasonComparisonDto(
    Guid? PreviousSeasonId,
    string? PreviousSeasonLabel,
    DateOnly? PreviousStartDate,
    Unit? PreviousYieldUnit,
    decimal? PreviousYieldPerHectare,
    decimal? YieldPerHectareDelta,
    bool SameYieldUnit,
    decimal? PreviousSupplyCost,
    decimal? SupplyCostDelta,
    decimal? SupplyCostPerM2,
    decimal? PreviousSupplyCostPerM2,
    decimal? SupplyCostPerM2Delta,
    decimal? PreviousRevenue,
    decimal? RevenueDelta,
    decimal? RevenuePerM2,
    decimal? PreviousRevenuePerM2,
    decimal? RevenuePerM2Delta,
    string? Message);

/// <summary>Dòng số liệu từng mùa vụ trong báo cáo sản xuất (FR-RPT-PROD-02..04).</summary>
public sealed record ProductionSeasonItemDto(
    ReportSeasonSummaryDto Season,
    decimal ActualYield,
    decimal TargetYield,
    decimal YieldDeviation,
    bool IsHighDeviation,
    decimal YieldPerM2,
    decimal YieldPerHectare,
    decimal? SupplyCost,
    decimal? Revenue,
    decimal? Profit,
    ProductionSeasonComparisonDto? PreviousComparison);

public sealed record ProductionReportResponse(
    ProductionOverviewDto Overview,
    decimal DeviationThreshold,
    PreviousSeasonCompareBy CompareBy,
    bool IncludesFinancialColumns,
    IReadOnlyList<ProductionSeasonItemDto> Seasons,
    IReadOnlyList<ProductionSeasonItemDto> CancelledSeasons);
