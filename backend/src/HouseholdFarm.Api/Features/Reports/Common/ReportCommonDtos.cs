using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Bộ lọc chung dùng cho các API xem và xuất báo cáo (FR-RPT-01).
/// </summary>
public record ReportFilter
{
    public DateOnly? From { get; init; }

    public DateOnly? To { get; init; }

    public Guid? SeasonId { get; init; }

    public Guid? PlotId { get; init; }

    public Guid? CropId { get; init; }
}

/// <summary>
/// Yêu cầu xuất báo cáo ra tệp (FR-RPT-04, FR-RPT-05). Kế thừa bộ lọc chung để dùng lại quy tắc kiểm tra ngày.
/// </summary>
public record ExportReportRequest : ReportFilter
{
    // Khai báo nullable để thiếu trường trong JSON không âm thầm nhận giá trị 0 của enum.
    public ExportFormat? Format { get; init; }
}

/// <summary>
/// Thông tin tóm tắt một mùa vụ trong báo cáo (FR-RPT-03, BR-RPT-03).
/// </summary>
public sealed record ReportSeasonSummaryDto(
    Guid SeasonId,
    Guid PlotId,
    string PlotCode,
    Guid CropId,
    string CropName,
    Unit YieldUnit,
    decimal AreaM2,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    SeasonStatus Status,
    bool IsProvisional);

public enum ReportColumnType
{
    Text,
    Integer,
    Decimal,
    Money,
    Percent,
    Date,
    DateTime,
}

public sealed record ReportColumn(string Header, ReportColumnType Type);

public sealed record ReportSummaryItem(string Label, string Value);

/// <summary>
/// Một bảng dữ liệu trong tài liệu xuất báo cáo; báo cáo có thể gồm nhiều bảng (ví dụ bảng chính và khối mùa vụ đã hủy).
/// </summary>
public sealed record ReportTableSection(
    string? Title,
    IReadOnlyList<ReportColumn> Columns,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    string? FooterNote = null);

/// <summary>
/// Mô hình tài liệu trung lập định dạng để <c>ExcelReportWriter</c> và bộ xuất PDF dùng chung (FR-RPT-04, FR-RPT-05).
/// </summary>
public sealed record ReportExportDocument(
    string Title,
    string FileNamePrefix,
    IReadOnlyList<ReportSummaryItem> FiltersSummary,
    IReadOnlyList<ReportSummaryItem> OverviewItems,
    IReadOnlyList<ReportTableSection> Sections,
    string SheetName = "Báo cáo");

public sealed record ExportedFileResult(byte[] Content, string ContentType, string FileName);
