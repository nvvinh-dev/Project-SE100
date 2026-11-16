using System.Globalization;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Reports.FarmTasks;

public sealed class FarmTaskReportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IConfiguration configuration,
    IReportAccessService access,
    IReportExportService exportService) : IFarmTaskReportService
{
    private static readonly StringComparer _vietnameseComparer = StringComparer.Create(
        CultureInfo.GetCultureInfo("vi-VN"),
        CompareOptions.IgnoreCase);

    private readonly TimeZoneInfo _timeZone = ReportSeasonQueryExtensions.GetAppTimeZone(configuration);

    public async Task<FarmTaskReportResponse> GetAsync(FarmTaskReportQuery query, CancellationToken ct)
    {
        access.EnsureCanViewReport(ReportType.FarmTask);

        // Lọc thời gian của công việc thực hiện theo scheduled_date (BR-RPT-02); bộ lọc mùa vụ/thửa/cây áp lên phạm vi mùa vụ.
        var scopeFilter = new ReportFilter
        {
            SeasonId = query.SeasonId,
            PlotId = query.PlotId,
            CropId = query.CropId,
        };

        var visibleSeasonIds = await db.Seasons.AsNoTracking()
            .VisibleToReport(currentUser, ReportType.FarmTask)
            .ApplyReportFilter(scopeFilter, _timeZone)
            .Select(s => s.Id)
            .ToListAsync(ct);

        var tasksQuery = db.FarmTasks.AsNoTracking()
            .Where(t => visibleSeasonIds.Contains(t.SeasonId));

        if (query.From is { } from)
        {
            tasksQuery = tasksQuery.Where(t => t.ScheduledDate >= from);
        }

        if (query.To is { } to)
        {
            tasksQuery = tasksQuery.Where(t => t.ScheduledDate <= to);
        }

        var tasks = await tasksQuery
            .Select(t => new TaskSnapshot(t.Status, t.ScheduledDate, t.AssigneeId))
            .ToListAsync(ct);

        var today = clock.Today;
        var cancelledCount = tasks.Count(t => t.Status == FarmTaskStatus.Cancelled);
        var activeTasks = tasks.Where(t => t.Status != FarmTaskStatus.Cancelled).ToList();

        var overview = new FarmTaskReportOverviewDto(
            PlannedCount: activeTasks.Count,
            CompletedCount: activeTasks.Count(t => t.Status == FarmTaskStatus.Done),
            OverdueCount: activeTasks.Count(t => IsOverdue(t, today)),
            PendingCount: activeTasks.Count(t => IsPending(t)),
            CancelledCount: cancelledCount);

        var rows = query.GroupBy == FarmTaskReportGroupBy.Assignee
            ? await GroupByAssigneeAsync(activeTasks, today, ct)
            : GroupByTime(activeTasks, today);

        return new FarmTaskReportResponse(query.GroupBy, overview, rows);
    }

    public async Task<ExportedFileResult> ExportAsync(ExportFarmTaskReportRequest request, CancellationToken ct)
    {
        var query = new FarmTaskReportQuery
        {
            From = request.From,
            To = request.To,
            SeasonId = request.SeasonId,
            PlotId = request.PlotId,
            CropId = request.CropId,
            GroupBy = request.GroupBy,
        };

        var report = await GetAsync(query, ct);
        var document = await BuildDocumentAsync(query, report, ct);

        return await exportService.ExportAsync(ReportType.FarmTask, request.Format!.Value, query, request, document, ct);
    }

    private static IReadOnlyList<FarmTaskReportRowDto> GroupByTime(
        IReadOnlyList<TaskSnapshot> tasks,
        DateOnly today) =>
        tasks
            .GroupBy(t => new { t.ScheduledDate.Year, t.ScheduledDate.Month })
            .OrderBy(g => g.Key.Year)
            .ThenBy(g => g.Key.Month)
            .Select(g =>
            {
                var items = g.ToList();
                var key = $"{g.Key.Year:D4}-{g.Key.Month:D2}";
                var label = $"{g.Key.Month:D2}/{g.Key.Year:D4}";
                return new FarmTaskReportRowDto(
                    key,
                    label,
                    IsUnassigned: false,
                    PlannedCount: items.Count,
                    CompletedCount: items.Count(t => t.Status == FarmTaskStatus.Done),
                    OverdueCount: items.Count(t => IsOverdue(t, today)),
                    PendingCount: items.Count(t => IsPending(t)));
            })
            .ToList();

    private async Task<IReadOnlyList<FarmTaskReportRowDto>> GroupByAssigneeAsync(
        IReadOnlyList<TaskSnapshot> tasks,
        DateOnly today,
        CancellationToken ct)
    {
        var nameByUserId = await db.Users.AsNoTracking()
            .InCurrentHousehold(currentUser)
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var assignedRows = new List<FarmTaskReportRowDto>();
        FarmTaskReportRowDto? unassignedRow = null;

        foreach (var group in tasks.GroupBy(t => t.AssigneeId))
        {
            var items = group.ToList();
            if (group.Key is null)
            {
                unassignedRow = new FarmTaskReportRowDto(
                    "unassigned",
                    "Chưa giao người",
                    IsUnassigned: true,
                    PlannedCount: items.Count,
                    CompletedCount: items.Count(t => t.Status == FarmTaskStatus.Done),
                    OverdueCount: items.Count(t => IsOverdue(t, today)),
                    PendingCount: items.Count(t => IsPending(t)));
                continue;
            }

            var label = nameByUserId.GetValueOrDefault(group.Key.Value, "Thành viên không xác định");
            assignedRows.Add(new FarmTaskReportRowDto(
                group.Key.Value.ToString(),
                label,
                IsUnassigned: false,
                PlannedCount: items.Count,
                CompletedCount: items.Count(t => t.Status == FarmTaskStatus.Done),
                OverdueCount: items.Count(t => IsOverdue(t, today)),
                PendingCount: items.Count(t => IsPending(t))));
        }

        // BR-RPT-08: sắp danh sách người theo tên, không sắp theo mức hoàn thành hay xếp hạng.
        var sorted = assignedRows
            .OrderBy(r => r.Label, _vietnameseComparer)
            .ThenBy(r => r.Key, StringComparer.Ordinal)
            .ToList();

        if (unassignedRow is not null)
        {
            sorted.Add(unassignedRow);
        }

        return sorted;
    }

    private async Task<ReportExportDocument> BuildDocumentAsync(
        FarmTaskReportQuery query,
        FarmTaskReportResponse report,
        CancellationToken ct)
    {
        var filters = new List<ReportSummaryItem>
        {
            new("Từ ngày (ngày dự kiến)", query.From?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Đến ngày (ngày dự kiến)", query.To?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Nhóm theo", query.GroupBy == FarmTaskReportGroupBy.Assignee ? "Người phụ trách" : "Thời gian (tháng)"),
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

        var overview = new List<ReportSummaryItem>
        {
            new("Đã lập", report.Overview.PlannedCount.ToString(CultureInfo.InvariantCulture)),
            new("Hoàn thành", report.Overview.CompletedCount.ToString(CultureInfo.InvariantCulture)),
            new("Trễ hạn", report.Overview.OverdueCount.ToString(CultureInfo.InvariantCulture)),
            new("Chưa thực hiện", report.Overview.PendingCount.ToString(CultureInfo.InvariantCulture)),
            new("Đã hủy (tách riêng)", report.Overview.CancelledCount.ToString(CultureInfo.InvariantCulture)),
        };

        var groupHeader = report.GroupBy == FarmTaskReportGroupBy.Assignee ? "Người phụ trách" : "Tháng";
        var columns = new[]
        {
            new ReportColumn(groupHeader, ReportColumnType.Text),
            new ReportColumn("Đã lập", ReportColumnType.Integer),
            new ReportColumn("Hoàn thành", ReportColumnType.Integer),
            new ReportColumn("Trễ hạn", ReportColumnType.Integer),
            new ReportColumn("Chưa thực hiện", ReportColumnType.Integer),
        };

        var tableRows = report.Rows
            .Select(r => (IReadOnlyList<object?>)[r.Label, r.PlannedCount, r.CompletedCount, r.OverdueCount, r.PendingCount])
            .ToList();

        var sectionTitle = report.GroupBy == FarmTaskReportGroupBy.Assignee
            ? "Thống kê công việc theo người phụ trách"
            : "Thống kê công việc theo thời gian";

        return new ReportExportDocument(
            "Báo cáo công việc",
            "bao-cao-cong-viec",
            filters,
            overview,
            [new(sectionTitle, columns, tableRows, "Báo cáo dùng để điều phối công việc.")],
            "Công việc");
    }

    private static bool IsPending(TaskSnapshot task) =>
        task.Status is FarmTaskStatus.Pending or FarmTaskStatus.WaitingForSupply;

    // Trễ hạn theo data model mục 6: chưa xong và scheduled_date < hôm nay theo giờ Việt Nam.
    private static bool IsOverdue(TaskSnapshot task, DateOnly today) =>
        IsPending(task) && task.ScheduledDate < today;

    private sealed record TaskSnapshot(FarmTaskStatus Status, DateOnly ScheduledDate, Guid? AssigneeId);
}
