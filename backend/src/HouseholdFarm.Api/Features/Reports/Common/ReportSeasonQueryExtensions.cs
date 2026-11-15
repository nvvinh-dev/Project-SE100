using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Các hàm dùng chung cho truy vấn mùa vụ và phân loại trạng thái trong báo cáo (FR-RPT-01, FR-RPT-03).
/// </summary>
public static class ReportSeasonQueryExtensions
{
    /// <summary>
    /// Lấy múi giờ nghiệp vụ từ cấu hình <c>App:TimeZone</c> để quy đổi mốc ngày khi lọc cột <c>...At</c>.
    /// </summary>
    public static TimeZoneInfo GetAppTimeZone(IConfiguration configuration)
    {
        var timeZoneId = configuration["App:TimeZone"]
            ?? throw new InvalidOperationException("Thiếu cấu hình App:TimeZone.");
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    /// <summary>
    /// Áp dụng phạm vi dữ liệu của từng loại báo cáo theo ma trận phân quyền (FR-RPT-02).
    /// </summary>
    public static IQueryable<Season> VisibleToReport(
        this IQueryable<Season> seasons,
        ICurrentUser currentUser,
        ReportType reportType) =>
        reportType switch
        {
            ReportType.Production or ReportType.FarmTask => seasons.VisibleTo(currentUser),
            ReportType.Supply => seasons.VisibleTo(currentUser, ExtraPermission.ManageStock),
            ReportType.Finance => seasons.VisibleTo(currentUser, ExtraPermission.ManageFinance),
            _ => throw new ArgumentOutOfRangeException(nameof(reportType), reportType, null),
        };

    /// <summary>
    /// Lọc mùa vụ theo bộ lọc báo cáo (FR-RPT-01) và quy tắc khoảng thời gian nghiệp vụ (BR-RPT-13).
    /// </summary>
    public static IQueryable<Season> ApplyReportFilter(
        this IQueryable<Season> seasons,
        ReportFilter filter,
        TimeZoneInfo timeZone)
    {
        if (filter.SeasonId is { } seasonId)
        {
            seasons = seasons.Where(s => s.Id == seasonId);
        }

        if (filter.PlotId is { } plotId)
        {
            seasons = seasons.Where(s => s.PlotId == plotId);
        }

        if (filter.CropId is { } cropId)
        {
            seasons = seasons.Where(s => s.CropId == cropId);
        }

        if (filter.To is { } to)
        {
            seasons = seasons.Where(s => s.StartDate <= to);
        }

        if (filter.From is { } from)
        {
            // BR-RPT-13: mùa vụ chưa kết thúc hoặc có ngày chốt/hủy theo giờ Việt Nam >= ngày đầu kỳ.
            var fromStartUtc = TimeZoneInfo.ConvertTimeToUtc(
                from.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified),
                timeZone);

            seasons = seasons.Where(s =>
                s.Status == SeasonStatus.Planned
                || s.Status == SeasonStatus.InProgress
                || (s.Status == SeasonStatus.Closed && s.ClosedAt >= fromStartUtc)
                || (s.Status == SeasonStatus.Cancelled && s.CancelledAt >= fromStartUtc));
        }

        return seasons;
    }

    // BR-RPT-03: mùa vụ chưa chốt tính là tạm tính; mùa vụ đã hủy tách thành khối riêng.
    public static bool IsProvisionalStatus(SeasonStatus status) =>
        status is SeasonStatus.Planned or SeasonStatus.InProgress;

    public static ReportSeasonSummaryDto ToSummaryDto(this Season season) =>
        new(
            season.Id,
            season.PlotId,
            season.Plot.Code,
            season.CropId,
            season.Crop.Name,
            season.Crop.YieldUnit,
            season.Plot.AreaM2,
            season.StartDate,
            season.ExpectedHarvestDate,
            season.Status,
            IsProvisionalStatus(season.Status));

    /// <summary>
    /// Tách riêng mùa vụ <c>Cancelled</c> khỏi danh sách mùa vụ chính theo BR-RPT-03.
    /// </summary>
    public static (IReadOnlyList<T> Active, IReadOnlyList<T> Cancelled) PartitionCancelled<T>(
        this IEnumerable<T> items,
        Func<T, SeasonStatus> statusSelector)
    {
        var active = new List<T>();
        var cancelled = new List<T>();

        foreach (var item in items)
        {
            if (statusSelector(item) == SeasonStatus.Cancelled)
            {
                cancelled.Add(item);
            }
            else
            {
                active.Add(item);
            }
        }

        return (active, cancelled);
    }
}
