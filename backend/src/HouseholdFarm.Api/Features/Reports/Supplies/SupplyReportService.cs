using System.Globalization;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;
using HouseholdFarm.Api.Features.Reports.Finance;
using HouseholdFarm.Api.Features.Supplies;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Reports.Supplies;

public sealed class SupplyReportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IReportAccessService access,
    IReportExportService exportService) : ISupplyReportService
{
    private const string StockValueNote = "Giá trị tồn tính theo giá vốn bình quân hiện tại.";
    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<SupplyReportResponse> GetAsync(SupplyReportQuery query, CancellationToken ct)
    {
        access.EnsureCanViewReport(ReportType.Supply);

        var supplies = db.Supplies.AsNoTracking();
        if (query.Group is { } group)
        {
            supplies = supplies.Where(s => s.Group == group);
        }

        var lowStock = await GetLowStockAsync(supplies, ct);
        var groups = await GetGroupsAsync(supplies, query.From, query.To, ct);
        var (reconciliation, manualOuts) = await GetReconciliationAsync(supplies, query.From, query.To, ct);

        return new SupplyReportResponse(
            lowStock,
            groups,
            groups.Sum(g => g.StockValue),
            reconciliation,
            manualOuts);
    }

    public async Task<ExportedFileResult> ExportAsync(ExportSupplyReportRequest request, CancellationToken ct)
    {
        var query = new SupplyReportQuery { From = request.From, To = request.To, Group = request.Group };
        var report = await GetAsync(query, ct);
        var document = BuildDocument(query, report);

        // Báo cáo vật tư chỉ lọc theo thời gian và nhóm; report_exports lưu khoảng thời gian, bộ lọc đầy đủ nằm trong filters.
        var filter = new ReportFilter { From = request.From, To = request.To };
        return await exportService.ExportAsync(ReportType.Supply, request.Format!.Value, filter, request, document, ct);
    }

    // FR-RPT-SUP-02, BR-SUP-12: cùng điều kiện với bộ lọc "Dưới ngưỡng" của danh sách vật tư; tồn âm xếp trước.
    private async Task<IReadOnlyList<SupplyReportLowStockItem>> GetLowStockAsync(
        IQueryable<Domain.Entities.Supply> supplies,
        CancellationToken ct) =>
        await supplies
            .WithStock(db.StockTransactions)
            .Where(r => r.Supply.Status == RecordStatus.Active && r.StockQuantity < r.Supply.MinStockLevel)
            .OrderBy(r => r.StockQuantity < 0 ? 0 : 1)
            .ThenBy(r => r.Supply.Name)
            .ThenBy(r => r.Supply.Id)
            .Select(r => new SupplyReportLowStockItem(
                r.Supply.Id,
                r.Supply.Code,
                r.Supply.Name,
                r.Supply.Group,
                r.Supply.Unit,
                r.StockQuantity,
                r.Supply.MinStockLevel))
            .ToListAsync(ct);

    // FR-RPT-SUP-01, BR-RPT-05: lượng theo từng vật tư, nhóm chỉ cộng giá trị.
    private async Task<IReadOnlyList<SupplyReportGroupItem>> GetGroupsAsync(
        IQueryable<Domain.Entities.Supply> supplies,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var supplyRows = await supplies
            .WithStock(db.StockTransactions)
            .Select(r => new
            {
                r.Supply.Id,
                r.Supply.Code,
                r.Supply.Name,
                r.Supply.Group,
                r.Supply.Unit,
                r.Supply.Status,
                r.Supply.AverageUnitCost,
                r.StockQuantity,
            })
            .ToListAsync(ct);
        var supplyIds = supplyRows.Select(s => s.Id).ToList();

        // BR-RPT-01, BR-RPT-02: loại Void, theo transaction_date. Giao dịch sau ngày cuối kỳ không thuộc tồn cuối kỳ.
        var movements = await db.StockTransactions.AsNoTracking()
            .Where(t => t.VoidedAt == null
                && supplyIds.Contains(t.SupplyId)
                && (to == null || t.TransactionDate <= to))
            .GroupBy(t => t.SupplyId)
            .Select(g => new
            {
                SupplyId = g.Key,
                Opening = g.Sum(t => from != null && t.TransactionDate < from ? t.Quantity : 0m),
                InQuantity = g.Sum(t => t.Type == StockTransactionType.In && (from == null || t.TransactionDate >= from)
                    ? t.Quantity : 0m),
                InValue = g.Sum(t => t.Type == StockTransactionType.In && (from == null || t.TransactionDate >= from)
                    ? t.TotalCost : 0m),
                OutQuantity = g.Sum(t => t.Type == StockTransactionType.Out && (from == null || t.TransactionDate >= from)
                    ? -t.Quantity : 0m),
                OutValue = g.Sum(t => t.Type == StockTransactionType.Out && (from == null || t.TransactionDate >= from)
                    ? t.TotalCost : 0m),
                AdjustmentQuantity = g.Sum(t => t.Type == StockTransactionType.Adjustment && (from == null || t.TransactionDate >= from)
                    ? t.Quantity : 0m),
                // total_cost luôn không âm nên lấy dấu theo lượng điều chỉnh.
                AdjustmentValue = g.Sum(t => t.Type == StockTransactionType.Adjustment && (from == null || t.TransactionDate >= from)
                    ? (t.Quantity < 0 ? -t.TotalCost : t.TotalCost) : 0m),
                Closing = g.Sum(t => t.Quantity),
            })
            .ToDictionaryAsync(m => m.SupplyId, ct);

        var rows = supplyRows
            .Select(s =>
            {
                movements.TryGetValue(s.Id, out var m);
                return (s.Group, Row: new SupplyReportRow(
                    s.Id,
                    s.Code,
                    s.Name,
                    s.Unit,
                    s.Status,
                    m?.Opening ?? 0m,
                    m?.InQuantity ?? 0m,
                    m?.InValue ?? 0m,
                    m?.OutQuantity ?? 0m,
                    m?.OutValue ?? 0m,
                    m?.AdjustmentQuantity ?? 0m,
                    m?.AdjustmentValue ?? 0m,
                    m?.Closing ?? 0m,
                    s.StockQuantity,
                    s.AverageUnitCost,
                    RoundMoney(s.StockQuantity * s.AverageUnitCost)));
            })
            // Vật tư ngừng sử dụng chỉ hiện khi còn số liệu, để bảng không đầy dòng trống.
            .Where(x => x.Row.Status == RecordStatus.Active || HasFigures(x.Row))
            .ToList();

        return rows
            .GroupBy(x => x.Group)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var items = g.Select(x => x.Row).OrderBy(r => r.Name).ThenBy(r => r.Code).ToList();
                return new SupplyReportGroupItem(
                    g.Key,
                    items,
                    items.Sum(r => r.InValue),
                    items.Sum(r => r.OutValue),
                    items.Sum(r => r.AdjustmentValue),
                    items.Sum(r => r.StockValue));
            })
            .ToList();
    }

    // FR-RPT-SUP-03, BR-RPT-06: đối chiếu giao dịch Out trong kỳ với dòng vật tư của công việc trỏ tới chúng.
    private async Task<(IReadOnlyList<SupplyReconciliationItem>, IReadOnlyList<SupplyManualOutItem>)> GetReconciliationAsync(
        IQueryable<Domain.Entities.Supply> supplies,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var seasons = db.Seasons.AsNoTracking().VisibleToReport(currentUser, ReportType.Supply);

        var outs = await db.StockTransactions.AsNoTracking()
            .Where(t => t.Type == StockTransactionType.Out
                && t.VoidedAt == null
                && t.SeasonId != null
                && supplies.Any(s => s.Id == t.SupplyId)
                && seasons.Any(s => s.Id == t.SeasonId)
                && (from == null || t.TransactionDate >= from)
                && (to == null || t.TransactionDate <= to))
            .Select(t => new
            {
                t.Id,
                t.SupplyId,
                SeasonId = t.SeasonId!.Value,
                t.TransactionDate,
                Quantity = -t.Quantity,
                t.CreatedById,
                t.CreatedAt,
                t.Note,
                // Đọc trực tiếp bảng của module công việc, chỉ đọc.
                FromTask = db.FarmTaskSupplyUsages.Any(u => u.StockTransactionId == t.Id),
                Recorded = db.FarmTaskSupplyUsages
                    .Where(u => u.StockTransactionId == t.Id)
                    .Sum(u => (decimal?)(u.ActualQuantity ?? 0m)) ?? 0m,
            })
            .ToListAsync(ct);

        if (outs.Count == 0)
        {
            return ([], []);
        }

        var supplyIds = outs.Select(o => o.SupplyId).Distinct().ToList();
        var seasonIds = outs.Select(o => o.SeasonId).Distinct().ToList();
        var userIds = outs.Where(o => !o.FromTask).Select(o => o.CreatedById).Distinct().ToList();

        var supplyInfo = await db.Supplies.AsNoTracking()
            .Where(s => supplyIds.Contains(s.Id))
            .Select(s => new { s.Id, s.Code, s.Name, s.Unit })
            .ToDictionaryAsync(s => s.Id, ct);
        var seasonInfo = (await db.Seasons.AsNoTracking()
                .Include(s => s.Plot)
                .Include(s => s.Crop)
                .Where(s => seasonIds.Contains(s.Id))
                .ToListAsync(ct))
            .ToDictionary(s => s.Id, s => s.ToSummaryDto());
        var userNames = await db.Users.AsNoTracking()
            .InCurrentHousehold(currentUser)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.FullName, ct);

        var reconciliation = outs
            .GroupBy(o => (o.SupplyId, o.SeasonId))
            .Select(g =>
            {
                var supply = supplyInfo[g.Key.SupplyId];
                var total = g.Sum(o => o.Quantity);
                var recorded = g.Sum(o => o.Recorded);
                return new SupplyReconciliationItem(
                    supply.Id,
                    supply.Code,
                    supply.Name,
                    supply.Unit,
                    seasonInfo[g.Key.SeasonId],
                    total,
                    g.Where(o => o.FromTask).Sum(o => o.Quantity),
                    g.Where(o => !o.FromTask).Sum(o => o.Quantity),
                    recorded,
                    total - recorded);
            })
            .OrderBy(i => i.SupplyName)
            .ThenBy(i => i.SupplyCode)
            .ThenByDescending(i => i.Season.StartDate)
            .ToList();

        var manualOuts = outs
            .Where(o => !o.FromTask)
            .OrderByDescending(o => o.TransactionDate)
            .ThenByDescending(o => o.CreatedAt)
            .Select(o =>
            {
                var supply = supplyInfo[o.SupplyId];
                return new SupplyManualOutItem(
                    o.Id,
                    o.TransactionDate,
                    supply.Id,
                    supply.Code,
                    supply.Name,
                    supply.Unit,
                    seasonInfo[o.SeasonId],
                    o.Quantity,
                    userNames.GetValueOrDefault(o.CreatedById) ?? string.Empty,
                    o.Note);
            })
            .ToList();

        return (reconciliation, manualOuts);
    }

    private static bool HasFigures(SupplyReportRow row) =>
        row.OpeningQuantity != 0m
        || row.InQuantity != 0m
        || row.OutQuantity != 0m
        || row.AdjustmentQuantity != 0m
        || row.ClosingQuantity != 0m
        || row.StockQuantity != 0m;

    private static ReportExportDocument BuildDocument(SupplyReportQuery query, SupplyReportResponse report)
    {
        var filters = new List<ReportSummaryItem>
        {
            new("Từ ngày", query.From?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Đến ngày", query.To?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Nhóm vật tư", query.Group is { } g ? FinanceReportLabels.Of(g) : "Tất cả"),
        };

        var overview = new List<ReportSummaryItem>
        {
            new("Vật tư dưới ngưỡng", report.LowStock.Count.ToString(CultureInfo.InvariantCulture)),
            new("Tổng giá trị tồn hiện tại", Money(report.TotalStockValue)),
        };

        // FR-RPT-SUP-02: phần vật tư dưới ngưỡng đứng đầu file xuất.
        var sections = new List<ReportTableSection>
        {
            new(
                "Vật tư dưới ngưỡng",
                [
                    new("Mã", ReportColumnType.Text),
                    new("Vật tư", ReportColumnType.Text),
                    new("Nhóm", ReportColumnType.Text),
                    new("Tồn hiện tại", ReportColumnType.Decimal),
                    new("Ngưỡng tối thiểu", ReportColumnType.Decimal),
                    new("Đơn vị", ReportColumnType.Text),
                    new("Tình trạng", ReportColumnType.Text),
                ],
                report.LowStock
                    .Select(i => (IReadOnlyList<object?>)
                    [
                        i.Code,
                        i.Name,
                        FinanceReportLabels.Of(i.Group),
                        i.StockQuantity,
                        i.MinStockLevel,
                        ReportLabels.Of(i.Unit),
                        i.StockQuantity < 0 ? "Tồn âm" : "Sắp hết",
                    ])
                    .ToList()),
        };

        var stockColumns = new[]
        {
            new ReportColumn("Mã", ReportColumnType.Text),
            new ReportColumn("Vật tư", ReportColumnType.Text),
            new ReportColumn("Đơn vị", ReportColumnType.Text),
            new ReportColumn("Tồn đầu kỳ", ReportColumnType.Decimal),
            new ReportColumn("Lượng nhập", ReportColumnType.Decimal),
            new ReportColumn("Giá trị nhập", ReportColumnType.Money),
            new ReportColumn("Lượng xuất", ReportColumnType.Decimal),
            new ReportColumn("Giá trị xuất", ReportColumnType.Money),
            new ReportColumn("Điều chỉnh", ReportColumnType.Decimal),
            new ReportColumn("Giá trị điều chỉnh", ReportColumnType.Money),
            new ReportColumn("Tồn cuối kỳ", ReportColumnType.Decimal),
            new ReportColumn("Tồn hiện tại", ReportColumnType.Decimal),
            new ReportColumn("Giá vốn bình quân", ReportColumnType.Money),
            new ReportColumn("Giá trị tồn", ReportColumnType.Money),
        };
        foreach (var group in report.Groups)
        {
            var rows = group.Supplies
                .Select(r => (IReadOnlyList<object?>)
                [
                    r.Code,
                    r.Name,
                    ReportLabels.Of(r.Unit),
                    r.OpeningQuantity,
                    r.InQuantity,
                    r.InValue,
                    r.OutQuantity,
                    r.OutValue,
                    r.AdjustmentQuantity,
                    r.AdjustmentValue,
                    r.ClosingQuantity,
                    r.StockQuantity,
                    r.AverageUnitCost,
                    r.StockValue,
                ])
                .ToList();
            // Dòng tổng của nhóm chỉ có giá trị, không cộng lượng vì khác đơn vị (BR-RPT-05).
            rows.Add(["", "Tổng nhóm", "", null, null, group.InValue, null, group.OutValue, null, group.AdjustmentValue, null, null, null, group.StockValue]);
            sections.Add(new($"Nhập, xuất, tồn: {FinanceReportLabels.Of(group.Group)}", stockColumns, rows, StockValueNote));
        }

        sections.Add(new(
            "Đối chiếu xuất kho với vật tư ghi trong công việc",
            [
                new("Vật tư", ReportColumnType.Text),
                new("Mùa vụ", ReportColumnType.Text),
                new("Đơn vị", ReportColumnType.Text),
                new("Tổng xuất", ReportColumnType.Decimal),
                new("Xuất từ công việc", ReportColumnType.Decimal),
                new("Xuất thủ công", ReportColumnType.Decimal),
                new("Lượng ghi trong công việc", ReportColumnType.Decimal),
                new("Chênh lệch", ReportColumnType.Decimal),
            ],
            report.Reconciliation
                .Select(i => (IReadOnlyList<object?>)
                [
                    i.SupplyName,
                    SeasonLabel(i.Season),
                    ReportLabels.Of(i.Unit),
                    i.TotalOutQuantity,
                    i.TaskOutQuantity,
                    i.ManualOutQuantity,
                    i.RecordedInTasksQuantity,
                    i.DifferenceQuantity,
                ])
                .ToList(),
            "Chênh lệch là lượng xuất kho không ghi vào công việc nào."));

        sections.Add(new(
            "Các lần xuất thủ công",
            [
                new("Ngày", ReportColumnType.Date),
                new("Vật tư", ReportColumnType.Text),
                new("Mùa vụ", ReportColumnType.Text),
                new("Lượng", ReportColumnType.Decimal),
                new("Đơn vị", ReportColumnType.Text),
                new("Người ghi", ReportColumnType.Text),
                new("Ghi chú", ReportColumnType.Text),
            ],
            report.ManualOuts
                .Select(i => (IReadOnlyList<object?>)
                [
                    i.TransactionDate,
                    i.SupplyName,
                    SeasonLabel(i.Season),
                    i.Quantity,
                    ReportLabels.Of(i.Unit),
                    i.CreatedByName,
                    i.Note,
                ])
                .ToList()));

        return new ReportExportDocument("Báo cáo vật tư", "bao-cao-vat-tu", filters, overview, sections, "Vật tư");
    }

    private static string SeasonLabel(ReportSeasonSummaryDto season) =>
        $"{season.CropName} · {season.PlotCode} · {season.StartDate.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}";

    private static decimal RoundMoney(decimal amount) => Math.Round(amount, 2, MidpointRounding.AwayFromZero);

    private static string Money(decimal amount) => $"{amount.ToString("#,##0.##", _vi)} ₫";
}
