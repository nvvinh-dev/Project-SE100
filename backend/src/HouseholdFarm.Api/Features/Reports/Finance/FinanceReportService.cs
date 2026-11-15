using System.Globalization;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Reports.Finance;

public sealed class FinanceReportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IConfiguration configuration,
    IReportAccessService access,
    ISeasonFinanceService seasonFinanceService,
    IReportExportService exportService) : IFinanceReportService
{
    private const string CashFlowAssumption = "Giả định: vật tư nhập kho được trả tiền ngay tại ngày nhập.";
    private const string CashFlowTimeOnly = "Dòng tiền chỉ xem theo thời gian, không áp dụng bộ lọc mùa vụ, thửa, cây trồng.";
    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    private readonly TimeZoneInfo _timeZone = ReportSeasonQueryExtensions.GetAppTimeZone(configuration);

    public async Task<FinanceReportResponse> GetAsync(FinanceReportQuery query, CancellationToken ct)
    {
        access.EnsureCanViewReport(ReportType.Finance);

        var from = query.From;
        var to = query.To;
        var hasScopeFilter = query.SeasonId is not null || query.PlotId is not null || query.CropId is not null;

        // BR-RPT-13 ý 1: mùa vụ khớp bộ lọc; mùa vụ đã hủy vẫn nằm trong các tổng theo nhóm, theo tháng (ý 4).
        var seasons = await db.Seasons
            .AsNoTracking()
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .VisibleToReport(currentUser, ReportType.Finance)
            .ApplyReportFilter(query, _timeZone)
            .OrderByDescending(s => s.StartDate)
            .ToListAsync(ct);
        var seasonIds = seasons.Select(s => s.Id).ToList();

        // BR-RPT-01, BR-RPT-02: loại bản ghi Void, lọc theo ngày nghiệp vụ.
        var supplyOuts = db.StockTransactions.AsNoTracking()
            .Where(t => t.Type == StockTransactionType.Out
                && t.VoidedAt == null
                && t.SeasonId != null
                && seasonIds.Contains(t.SeasonId.Value)
                && (from == null || t.TransactionDate >= from)
                && (to == null || t.TransactionDate <= to));
        var seasonExpenses = db.Expenses.AsNoTracking()
            .Where(e => e.VoidedAt == null
                && e.SeasonId != null
                && seasonIds.Contains(e.SeasonId.Value)
                && (from == null || e.ExpenseDate >= from)
                && (to == null || e.ExpenseDate <= to));
        var generalExpenses = db.Expenses.AsNoTracking()
            .Where(e => e.VoidedAt == null
                && e.SeasonId == null
                && (from == null || e.ExpenseDate >= from)
                && (to == null || e.ExpenseDate <= to));
        var sales = db.Sales.AsNoTracking()
            .Where(s => s.VoidedAt == null
                && seasonIds.Contains(s.SeasonId)
                && (from == null || s.SaleDate >= from)
                && (to == null || s.SaleDate <= to));

        var supplyCosts = (await supplyOuts
                .GroupBy(t => t.Supply.Group)
                .Select(g => new FinanceSupplyCostItem(g.Key, g.Sum(t => t.TotalCost)))
                .ToListAsync(ct))
            .OrderByDescending(i => i.Amount)
            .ToList();

        var expenseCosts = (await seasonExpenses
                .GroupBy(e => e.Category)
                .Select(g => new FinanceExpenseCostItem(g.Key, g.Sum(e => e.Amount)))
                .ToListAsync(ct))
            .OrderByDescending(i => i.Amount)
            .ToList();

        // BR-RPT-13 ý 3: chi phí chung của hộ chỉ có khi không lọc theo mùa vụ, thửa, cây trồng.
        decimal? generalExpenseAmount = hasScopeFilter ? null : await generalExpenses.SumAsync(e => e.Amount, ct);

        var revenueByChannel = (await sales
                .GroupBy(s => s.Channel)
                .Select(g => new FinanceRevenueChannelItem(g.Key, g.Sum(s => Math.Round(s.Quantity * s.UnitPrice, 2))))
                .ToListAsync(ct))
            .OrderByDescending(i => i.Amount)
            .ToList();

        var totalCost = supplyCosts.Sum(i => i.Amount) + expenseCosts.Sum(i => i.Amount) + (generalExpenseAmount ?? 0m);
        var totalRevenue = revenueByChannel.Sum(i => i.Amount);

        // Số liệu từng mùa vụ lấy qua hàm đọc dùng chung, tính cho cả mùa (BR-RPT-13 ý 2).
        var finances = (await seasonFinanceService.GetAsync(seasonIds, ct)).ToDictionary(f => f.SeasonId);
        var seasonItems = seasons
            .Select(s =>
            {
                var f = finances[s.Id];
                return new FinanceSeasonItem(
                    s.ToSummaryDto(), f.SupplyCost, f.ExpenseAmount, f.Revenue, f.PaidAmount, f.OutstandingAmount, f.Profit);
            })
            .ToList();
        var (activeSeasons, cancelledSeasons) = seasonItems.PartitionCancelled(i => i.Season.Status);

        // BR-RPT-03: lợi nhuận tách hai dòng đã chốt và tạm tính; mùa vụ đã hủy không nằm trong hai tổng này.
        var closedProfit = activeSeasons.Where(i => i.Season.Status == SeasonStatus.Closed).Sum(i => i.Profit);
        var provisionalProfit = activeSeasons.Where(i => i.Season.IsProvisional).Sum(i => i.Profit);

        var cashFlow = await GetCashFlowAsync(from, to, ct);
        var monthly = await GetMonthlyAsync(supplyOuts, seasonExpenses, hasScopeFilter ? null : generalExpenses, sales, from, to, ct);

        return new FinanceReportResponse(
            supplyCosts,
            expenseCosts,
            generalExpenseAmount,
            totalCost,
            revenueByChannel,
            totalRevenue,
            activeSeasons,
            cancelledSeasons,
            closedProfit,
            provisionalProfit,
            cashFlow,
            monthly);
    }

    public async Task<ExportedFileResult> ExportAsync(ExportReportRequest request, CancellationToken ct)
    {
        var query = new FinanceReportQuery
        {
            From = request.From,
            To = request.To,
            SeasonId = request.SeasonId,
            PlotId = request.PlotId,
            CropId = request.CropId,
        };
        var report = await GetAsync(query, ct);
        var document = await BuildDocumentAsync(query, report, ct);

        return await exportService.ExportAsync(ReportType.Finance, request.Format!.Value, query, request, document, ct);
    }

    // BR-RPT-07: chỉ lọc theo thời gian vì giao dịch nhập kho không gắn mùa vụ.
    private async Task<FinanceCashFlow> GetCashFlowAsync(DateOnly? from, DateOnly? to, CancellationToken ct)
    {
        var stockInAmount = await db.StockTransactions.AsNoTracking()
            .Where(t => t.Type == StockTransactionType.In
                && t.VoidedAt == null
                && (from == null || t.TransactionDate >= from)
                && (to == null || t.TransactionDate <= to))
            .SumAsync(t => t.TotalCost, ct);

        var expenseAmount = await db.Expenses.AsNoTracking()
            .Where(e => e.VoidedAt == null
                && (from == null || e.ExpenseDate >= from)
                && (to == null || e.ExpenseDate <= to))
            .SumAsync(e => e.Amount, ct);

        var cashIn = await db.Payments.AsNoTracking()
            .Where(p => p.VoidedAt == null
                && p.Sale.VoidedAt == null
                && (from == null || p.PaymentDate >= from)
                && (to == null || p.PaymentDate <= to))
            .SumAsync(p => p.Amount, ct);

        // Doanh thu chưa thu là công nợ tính đến ngày cuối kỳ: không trừ thanh toán ghi sau ngày đó.
        var endDate = to ?? clock.Today;
        var revenueToEnd = await db.Sales.AsNoTracking()
            .Where(s => s.VoidedAt == null && s.SaleDate <= endDate)
            .SumAsync(s => Math.Round(s.Quantity * s.UnitPrice, 2), ct);
        var paidToEnd = await db.Payments.AsNoTracking()
            .Where(p => p.VoidedAt == null
                && p.Sale.VoidedAt == null
                && p.Sale.SaleDate <= endDate
                && p.PaymentDate <= endDate)
            .SumAsync(p => p.Amount, ct);

        return new FinanceCashFlow(
            stockInAmount,
            expenseAmount,
            stockInAmount + expenseAmount,
            cashIn,
            revenueToEnd - paidToEnd);
    }

    // FR-RPT-FIN-06, BR-RPT-13 ý 6: chi phí theo tháng là giá trị xuất kho và chi phí ghi tay, không phải tiền chi.
    private async Task<IReadOnlyList<FinanceMonthItem>> GetMonthlyAsync(
        IQueryable<Domain.Entities.StockTransaction> supplyOuts,
        IQueryable<Domain.Entities.Expense> seasonExpenses,
        IQueryable<Domain.Entities.Expense>? generalExpenses,
        IQueryable<Domain.Entities.Sale> sales,
        DateOnly? from,
        DateOnly? to,
        CancellationToken ct)
    {
        var costs = new Dictionary<(int Year, int Month), decimal>();
        var revenues = new Dictionary<(int Year, int Month), decimal>();

        static void Add(Dictionary<(int, int), decimal> target, int year, int month, decimal amount) =>
            target[(year, month)] = target.GetValueOrDefault((year, month)) + amount;

        foreach (var row in await supplyOuts
                     .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                     .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(t => t.TotalCost) })
                     .ToListAsync(ct))
        {
            Add(costs, row.Year, row.Month, row.Total);
        }

        foreach (var expenses in generalExpenses is null ? [seasonExpenses] : new[] { seasonExpenses, generalExpenses })
        {
            foreach (var row in await expenses
                         .GroupBy(e => new { e.ExpenseDate.Year, e.ExpenseDate.Month })
                         .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(e => e.Amount) })
                         .ToListAsync(ct))
            {
                Add(costs, row.Year, row.Month, row.Total);
            }
        }

        foreach (var row in await sales
                     .GroupBy(s => new { s.SaleDate.Year, s.SaleDate.Month })
                     .Select(g => new { g.Key.Year, g.Key.Month, Total = g.Sum(s => Math.Round(s.Quantity * s.UnitPrice, 2)) })
                     .ToListAsync(ct))
        {
            Add(revenues, row.Year, row.Month, row.Total);
        }

        // Đủ mọi tháng trong kỳ, kể cả tháng không phát sinh; không có ngày đầu kỳ thì bắt đầu từ tháng có dữ liệu sớm nhất.
        var dataMonths = costs.Keys.Concat(revenues.Keys).Select(k => new DateOnly(k.Year, k.Month, 1)).ToList();
        DateOnly? start = from is { } f ? new DateOnly(f.Year, f.Month, 1) : dataMonths.Count > 0 ? dataMonths.Min() : null;
        if (start is null)
        {
            return [];
        }

        var endDay = to ?? clock.Today;
        var end = new DateOnly(endDay.Year, endDay.Month, 1);
        if (dataMonths.Count > 0 && dataMonths.Max() > end)
        {
            end = dataMonths.Max();
        }

        var result = new List<FinanceMonthItem>();
        for (var month = start.Value; month <= end; month = month.AddMonths(1))
        {
            var key = (month.Year, month.Month);
            result.Add(new FinanceMonthItem(
                month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
                revenues.GetValueOrDefault(key),
                costs.GetValueOrDefault(key)));
        }

        return result;
    }

    private async Task<ReportExportDocument> BuildDocumentAsync(
        FinanceReportQuery query,
        FinanceReportResponse report,
        CancellationToken ct)
    {
        var filters = new List<ReportSummaryItem>
        {
            new("Từ ngày", query.From?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
            new("Đến ngày", query.To?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? "Không giới hạn"),
        };
        if (query.SeasonId is { } seasonId)
        {
            var season = await db.Seasons.AsNoTracking()
                .Where(s => s.Id == seasonId)
                .Select(s => s.Crop.Name + " · " + s.Plot.Code)
                .FirstOrDefaultAsync(ct);
            filters.Add(new("Mùa vụ", season ?? "Không tìm thấy"));
        }

        if (query.PlotId is { } plotId)
        {
            var plot = await db.Plots.AsNoTracking().Where(p => p.Id == plotId).Select(p => p.Code).FirstOrDefaultAsync(ct);
            filters.Add(new("Thửa đất", plot ?? "Không tìm thấy"));
        }

        if (query.CropId is { } cropId)
        {
            var crop = await db.Crops.AsNoTracking().Where(c => c.Id == cropId).Select(c => c.Name).FirstOrDefaultAsync(ct);
            filters.Add(new("Cây trồng", crop ?? "Không tìm thấy"));
        }

        var overview = new List<ReportSummaryItem>
        {
            new("Tổng chi phí", Money(report.TotalCost)),
            new("Tổng doanh thu", Money(report.TotalRevenue)),
            new("Lợi nhuận đã chốt", Money(report.ClosedProfit)),
            new("Lợi nhuận tạm tính", Money(report.ProvisionalProfit)),
        };

        var moneyColumns = new[] { new ReportColumn("Nhóm", ReportColumnType.Text), new ReportColumn("Số tiền", ReportColumnType.Money) };
        var costRows = report.SupplyCosts
            .Select(i => Row($"Vật tư: {FinanceReportLabels.Of(i.Group)}", i.Amount))
            .Concat(report.ExpenseCosts.Select(i => Row($"Chi phí ghi tay: {FinanceReportLabels.Of(i.Category)}", i.Amount)))
            .ToList();
        if (report.GeneralExpenseAmount is { } general)
        {
            costRows.Add(Row("Chi phí chung của hộ", general));
        }

        costRows.Add(Row("Tổng chi phí", report.TotalCost));

        var revenueRows = report.RevenueByChannel
            .Select(i => Row(FinanceReportLabels.Of(i.Channel), i.Amount))
            .Append(Row("Tổng doanh thu", report.TotalRevenue))
            .ToList();

        var seasonColumns = new[]
        {
            new ReportColumn("Mùa vụ", ReportColumnType.Text),
            new ReportColumn("Trạng thái", ReportColumnType.Text),
            new ReportColumn("Chi phí vật tư", ReportColumnType.Money),
            new ReportColumn("Chi phí ghi tay", ReportColumnType.Money),
            new ReportColumn("Doanh thu", ReportColumnType.Money),
            new ReportColumn("Đã thu", ReportColumnType.Money),
            new ReportColumn("Công nợ", ReportColumnType.Money),
            new ReportColumn("Lợi nhuận", ReportColumnType.Money),
        };

        var cashFlowRows = new List<IReadOnlyList<object?>>
        {
            Row("Tiền chi cho nhập kho", report.CashFlow.StockInAmount),
            Row("Tiền chi cho chi phí ghi tay", report.CashFlow.ExpenseAmount),
            Row("Tổng tiền chi", report.CashFlow.CashOut),
            Row("Tiền thu", report.CashFlow.CashIn),
            Row("Doanh thu chưa thu (đến ngày cuối kỳ)", report.CashFlow.UnpaidRevenue),
        };

        var sections = new List<ReportTableSection>
        {
            new("Chi phí theo nhóm", moneyColumns, costRows),
            new("Doanh thu theo kênh bán", [new("Kênh bán", ReportColumnType.Text), new("Số tiền", ReportColumnType.Money)], revenueRows),
            new(
                "Lợi nhuận theo mùa vụ",
                seasonColumns,
                report.Seasons.Select(SeasonRow).ToList(),
                "Lợi nhuận của mùa vụ chưa chốt là số tạm tính."),
        };
        if (report.CancelledSeasons.Count > 0)
        {
            sections.Add(new("Mùa vụ đã hủy", seasonColumns, report.CancelledSeasons.Select(SeasonRow).ToList()));
        }

        sections.Add(new(
            "Dòng tiền",
            [new("Khoản", ReportColumnType.Text), new("Số tiền", ReportColumnType.Money)],
            cashFlowRows,
            $"{CashFlowAssumption} {CashFlowTimeOnly}"));
        sections.Add(new(
            "Doanh thu và chi phí theo tháng",
            [
                new("Tháng", ReportColumnType.Text),
                new("Doanh thu", ReportColumnType.Money),
                new("Chi phí", ReportColumnType.Money),
            ],
            report.Monthly.Select(m => (IReadOnlyList<object?>)[MonthLabel(m.Month), m.Revenue, m.Cost]).ToList(),
            "Chi phí theo tháng là giá trị xuất kho và chi phí ghi tay, không phải tiền chi của dòng tiền."));

        return new ReportExportDocument(
            "Báo cáo chi phí - doanh thu",
            "bao-cao-chi-phi-doanh-thu",
            filters,
            overview,
            sections,
            "Chi phí - doanh thu");
    }

    private static IReadOnlyList<object?> Row(string label, decimal amount) => [label, amount];

    private static IReadOnlyList<object?> SeasonRow(FinanceSeasonItem item) =>
    [
        $"{item.Season.CropName} · {item.Season.PlotCode}",
        item.Season.IsProvisional
            ? $"{FinanceReportLabels.Of(item.Season.Status)} (tạm tính)"
            : FinanceReportLabels.Of(item.Season.Status),
        item.SupplyCost,
        item.ExpenseAmount,
        item.Revenue,
        item.PaidAmount,
        item.OutstandingAmount,
        item.Profit,
    ];

    private static string MonthLabel(string month) => $"{month[5..]}/{month[..4]}";

    private static string Money(decimal amount) => $"{amount.ToString("#,##0.##", _vi)} ₫";
}
