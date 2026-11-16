using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Reports;

/// <summary>
/// Báo cáo chi phí - doanh thu, dòng tiền (FR-RPT-FIN-01, FR-RPT-FIN-03, FR-RPT-FIN-04, FR-RPT-FIN-06,
/// BR-RPT-01, BR-RPT-02, BR-RPT-03, BR-RPT-07, BR-RPT-13).
/// </summary>
/// <remarks>
/// Dữ liệu chung (kỳ lọc 01/10/2026 đến 30/11/2026):
/// mùa A đang canh tác: xuất phân 15 kg × 22.000 = 330.000 (05/10), chi nhân công 150.000 (10/10),
/// bán thương lái 500 × 8.000 = 4.000.000 (01/11), bán thêm 10 × 1.000 = 10.000 ngày 05/12 (ngoài kỳ);
/// mùa B đã chốt (31/10): xuất giống 10 × 5.000 = 50.000 (20/10), chi vận chuyển 40.000 (12/10),
/// bán cho khách không ghi kênh 100 × 10.000 = 1.000.000 (15/11);
/// mùa C đã hủy (25/10): xuất phân 2 × 22.000 = 44.000 (21/10);
/// chi phí chung điện nước 70.000 (15/10); nhập kho 200.000 + 240.000 (01/10), 300.000 ngày 05/12 (ngoài kỳ);
/// thanh toán 1.500.000 (02/11) và 500.000 (10/12, ngoài kỳ) cho giao dịch bán thương lái.
/// Mỗi loại có thêm một bản ghi Void không được tính.
/// </remarks>
public class FinanceReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string FinancePath = "/api/reports/finance";
    private const string Period = "from=2026-10-01&to=2026-11-30";

    [Fact(DisplayName = "FR-RPT-FIN-01: chi phí theo nhóm, doanh thu theo kênh trong kỳ, có chi phí chung, loại bản ghi Void")]
    public async Task Get_GroupsCostsAndRevenueWithinPeriod()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        var supplyCosts = report.GetProperty("supplyCosts").EnumerateArray()
            .Select(i => (i.GetProperty("group").GetString(), i.GetProperty("amount").GetDecimal())).ToList();
        Assert.Equal([("Fertilizer", 374_000m), ("Seed", 50_000m)], supplyCosts);
        var expenseCosts = report.GetProperty("expenseCosts").EnumerateArray()
            .Select(i => (i.GetProperty("category").GetString(), i.GetProperty("amount").GetDecimal())).ToList();
        Assert.Equal([("Labor", 150_000m), ("Transport", 40_000m)], expenseCosts);
        Assert.Equal(70_000m, report.GetProperty("generalExpenseAmount").GetDecimal());
        Assert.Equal(684_000m, report.GetProperty("totalCost").GetDecimal());

        var revenue = report.GetProperty("revenueByChannel").EnumerateArray()
            .Select(i => (i.GetProperty("channel").ValueKind == JsonValueKind.Null ? null : i.GetProperty("channel").GetString(),
                i.GetProperty("amount").GetDecimal()))
            .ToList();
        Assert.Equal([("Trader", 4_000_000m), (null, 1_000_000m)], revenue);
        Assert.Equal(5_000_000m, report.GetProperty("totalRevenue").GetDecimal());
    }

    [Fact(DisplayName = "FR-RPT-FIN-04: lợi nhuận cả mùa qua hàm dùng chung, tách đã chốt, tạm tính; mùa vụ đã hủy tách riêng")]
    public async Task Get_SplitsClosedProvisionalAndCancelledProfit()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        var seasons = report.GetProperty("seasons").EnumerateArray().ToList();
        Assert.Equal(2, seasons.Count);
        var a = seasons.Single(s => SeasonId(s) == data.SeasonA);
        // Mùa A tính cả giao dịch bán ngày 05/12 nằm ngoài kỳ: 4.010.000 − 330.000 − 150.000.
        Assert.Equal(3_530_000m, a.GetProperty("profit").GetDecimal());
        Assert.Equal(4_010_000m, a.GetProperty("revenue").GetDecimal());
        Assert.True(a.GetProperty("season").GetProperty("isProvisional").GetBoolean());
        var b = seasons.Single(s => SeasonId(s) == data.SeasonB);
        Assert.Equal(910_000m, b.GetProperty("profit").GetDecimal());
        Assert.False(b.GetProperty("season").GetProperty("isProvisional").GetBoolean());

        var cancelled = Assert.Single(report.GetProperty("cancelledSeasons").EnumerateArray());
        Assert.Equal(data.SeasonC, SeasonId(cancelled));
        Assert.Equal(-44_000m, cancelled.GetProperty("profit").GetDecimal());

        Assert.Equal(910_000m, report.GetProperty("closedProfit").GetDecimal());
        Assert.Equal(3_530_000m, report.GetProperty("provisionalProfit").GetDecimal());
    }

    [Fact(DisplayName = "BR-RPT-13: lọc theo mùa vụ thì không có chi phí chung, chỉ số liệu của mùa đó; dòng tiền không đổi")]
    public async Task Get_FilterBySeason_OmitsGeneralExpenseAndKeepsCashFlow()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, $"{Period}&seasonId={data.SeasonA}", ct);
        var all = await GetReportAsync(data.Household.Owner, Period, ct);

        Assert.Equal(JsonValueKind.Null, report.GetProperty("generalExpenseAmount").ValueKind);
        Assert.Equal(480_000m, report.GetProperty("totalCost").GetDecimal());
        Assert.Equal(4_000_000m, report.GetProperty("totalRevenue").GetDecimal());
        Assert.Equal(data.SeasonA, SeasonId(Assert.Single(report.GetProperty("seasons").EnumerateArray())));
        Assert.Equal(all.GetProperty("cashFlow").GetRawText(), report.GetProperty("cashFlow").GetRawText());
    }

    [Fact(DisplayName = "FR-RPT-FIN-03, BR-RPT-07: dòng tiền trong kỳ, nhập kho và thanh toán ngoài kỳ không tính, công nợ tính đến cuối kỳ")]
    public async Task Get_ComputesCashFlow()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeAsync(ct);

        var cashFlow = (await GetReportAsync(data.Household.Owner, Period, ct)).GetProperty("cashFlow");

        Assert.Equal(440_000m, cashFlow.GetProperty("stockInAmount").GetDecimal());
        Assert.Equal(260_000m, cashFlow.GetProperty("expenseAmount").GetDecimal());
        Assert.Equal(700_000m, cashFlow.GetProperty("cashOut").GetDecimal());
        Assert.Equal(1_500_000m, cashFlow.GetProperty("cashIn").GetDecimal());
        // Bán đến 30/11: 5.000.000; đã thu đến 30/11: 1.500.000 (khoản 500.000 ngày 10/12 không trừ).
        Assert.Equal(3_500_000m, cashFlow.GetProperty("unpaidRevenue").GetDecimal());
    }

    [Fact(DisplayName = "FR-RPT-FIN-06: đủ từng tháng trong kỳ, tổng các tháng khớp tổng chi phí và doanh thu của báo cáo")]
    public async Task Get_MonthlyMatchesTotals()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        var months = report.GetProperty("monthly").EnumerateArray()
            .Select(m => (m.GetProperty("month").GetString(), m.GetProperty("revenue").GetDecimal(), m.GetProperty("cost").GetDecimal()))
            .ToList();
        Assert.Equal([("2026-10", 0m, 684_000m), ("2026-11", 5_000_000m, 0m)], months);
        Assert.Equal(report.GetProperty("totalCost").GetDecimal(), months.Sum(m => m.Item3));
        Assert.Equal(report.GetProperty("totalRevenue").GetDecimal(), months.Sum(m => m.Item2));
    }

    [Fact(DisplayName = "FR-RPT-02: Thành viên có TC xem được; Thành viên không có TC, Nhân công bị FORBIDDEN")]
    public async Task Get_ChecksFinancePermission()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var allowed = await factory.CreateClientFor(household.MemberWithAllPermissions).GetAsync($"{FinancePath}?{Period}", ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            var response = await factory.CreateClientFor(user).GetAsync($"{FinancePath}?{Period}", ct);
            await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "FR-RPT-04, FR-RPT-06: xuất Excel trả file, lưu report_exports và nhật ký Report.Export")]
    public async Task Export_ReturnsWorkbookAndRecordsExport()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeAsync(ct);
        var client = factory.CreateClientFor(data.Household.Owner);

        var response = await client.PostAsJsonAsync(
            $"{FinancePath}/export",
            new { format = "Excel", from = "2026-10-01", to = "2026-11-30" },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            response.Content.Headers.ContentType?.MediaType);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Báo cáo chi phí - doanh thu", sheet.Cell(1, 1).GetString());
        Assert.Contains(sheet.CellsUsed(), c => c.GetString() == "Dòng tiền");
        Assert.Contains(sheet.CellsUsed(), c => c.GetString() == "Chi phí chung của hộ");

        await using var db = factory.Postgres.CreateDbContext();
        var export = await db.ReportExports.IgnoreQueryFilters()
            .SingleAsync(r => r.HouseholdId == data.Household.Household.Id, ct);
        Assert.Equal(ReportType.Finance, export.ReportType);
        Assert.Equal(new DateOnly(2026, 10, 1), export.FromDate);
        Assert.Equal(new DateOnly(2026, 11, 30), export.ToDate);
        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres, data.Household.Household.Id, AuditActions.ReportExport, export.Id, ct);
    }

    [Fact(DisplayName = "FR-RPT-02: Thành viên không có TC xuất báo cáo tài chính bị FORBIDDEN, không lưu lần xuất")]
    public async Task Export_WithoutFinancePermission_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Member)
            .PostAsJsonAsync($"{FinancePath}/export", new { format = "Excel" }, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.ReportExports.IgnoreQueryFilters().AnyAsync(r => r.HouseholdId == household.Household.Id, ct));
    }

    private sealed record Scenario(TestHousehold Household, Guid SeasonA, Guid SeasonB, Guid SeasonC);

    private async Task<JsonElement> GetReportAsync(ApplicationUser user, string queryString, CancellationToken ct)
    {
        var response = await factory.CreateClientFor(user).GetAsync($"{FinancePath}?{queryString}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data");
    }

    private static Guid SeasonId(JsonElement item) => item.GetProperty("season").GetProperty("seasonId").GetGuid();

    private async Task<Scenario> ArrangeAsync(CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plotA = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plotB = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var plotC = await TestData.CreatePlotAsync(factory.Postgres, household, "TD003", ct);
        var seasonA = await TestData.CreateSeasonAsync(factory.Postgres, household, plotA, SeasonStatus.InProgress, ct);
        var seasonB = await TestData.CreateSeasonAsync(factory.Postgres, household, plotB, SeasonStatus.Closed, ct);
        var seasonC = await TestData.CreateSeasonAsync(factory.Postgres, household, plotC, SeasonStatus.Cancelled, ct);
        var fertilizer = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, group: SupplyGroup.Fertilizer, averageUnitCost: 22_000m);
        var seed = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT002", ct, group: SupplyGroup.Seed, averageUnitCost: 5_000m);
        var customer = new Customer { Name = "Chị Lan", Status = RecordStatus.Active };

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Customers.Add(customer);
        var closed = await db.Seasons.SingleAsync(s => s.Id == seasonB.Id, ct);
        closed.ClosedAt = new DateTime(2026, 10, 31, 3, 0, 0, DateTimeKind.Utc);
        var cancelledSeason = await db.Seasons.SingleAsync(s => s.Id == seasonC.Id, ct);
        cancelledSeason.CancelledAt = new DateTime(2026, 10, 25, 3, 0, 0, DateTimeKind.Utc);
        cancelledSeason.CancelReason = "Mất mùa";

        db.StockTransactions.AddRange(
            Stock(fertilizer, StockTransactionType.In, 10m, 20_000m, new DateOnly(2026, 10, 1)),
            Stock(fertilizer, StockTransactionType.In, 10m, 24_000m, new DateOnly(2026, 10, 1)),
            Stock(fertilizer, StockTransactionType.In, 10m, 30_000m, new DateOnly(2026, 12, 5)),
            Stock(fertilizer, StockTransactionType.In, 5m, 99_000m, new DateOnly(2026, 10, 2), voided: true),
            Stock(fertilizer, StockTransactionType.Out, -15m, 22_000m, new DateOnly(2026, 10, 5), seasonA.Id),
            Stock(fertilizer, StockTransactionType.Out, -3m, 22_000m, new DateOnly(2026, 10, 6), seasonA.Id, voided: true),
            Stock(seed, StockTransactionType.Out, -10m, 5_000m, new DateOnly(2026, 10, 20), seasonB.Id),
            Stock(fertilizer, StockTransactionType.Out, -2m, 22_000m, new DateOnly(2026, 10, 21), seasonC.Id));

        db.Expenses.AddRange(
            Expense(seasonA.Id, ExpenseCategory.Labor, 150_000m, new DateOnly(2026, 10, 10)),
            Expense(seasonB.Id, ExpenseCategory.Transport, 40_000m, new DateOnly(2026, 10, 12)),
            Expense(null, ExpenseCategory.Utilities, 70_000m, new DateOnly(2026, 10, 15)),
            Expense(seasonA.Id, ExpenseCategory.Labor, 999_000m, new DateOnly(2026, 10, 16), voided: true));

        var traderSale = Sale(seasonA.Id, SalesChannel.Trader, null, 500m, 8_000m, new DateOnly(2026, 11, 1));
        var voidedSale = Sale(seasonA.Id, SalesChannel.Retail, null, 50m, 9_000m, new DateOnly(2026, 11, 3), voided: true);
        db.Sales.AddRange(
            traderSale,
            voidedSale,
            Sale(seasonA.Id, SalesChannel.Trader, null, 10m, 1_000m, new DateOnly(2026, 12, 5)),
            Sale(seasonB.Id, null, customer.Id, 100m, 10_000m, new DateOnly(2026, 11, 15)));

        db.Payments.AddRange(
            Payment(traderSale.Id, 1_500_000m, new DateOnly(2026, 11, 2)),
            Payment(traderSale.Id, 500_000m, new DateOnly(2026, 12, 10)),
            Payment(traderSale.Id, 200_000m, new DateOnly(2026, 11, 5), voided: true),
            Payment(voidedSale.Id, 450_000m, new DateOnly(2026, 11, 3), voided: true));

        await db.SaveChangesAsync(ct);
        return new Scenario(household, seasonA.Id, seasonB.Id, seasonC.Id);
    }

    private static DateTime? VoidedAt(bool voided) => voided ? new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) : null;

    private static StockTransaction Stock(
        Supply supply,
        StockTransactionType type,
        decimal quantity,
        decimal unitCost,
        DateOnly date,
        Guid? seasonId = null,
        bool voided = false) =>
        new()
        {
            SupplyId = supply.Id,
            Type = type,
            TransactionDate = date,
            Quantity = quantity,
            UnitCost = unitCost,
            TotalCost = Math.Abs(quantity) * unitCost,
            SeasonId = seasonId,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = VoidedAt(voided),
            VoidReason = voided ? "Ghi nhầm" : null,
        };

    private static Expense Expense(Guid? seasonId, ExpenseCategory category, decimal amount, DateOnly date, bool voided = false) =>
        new()
        {
            SeasonId = seasonId,
            Category = category,
            Amount = amount,
            ExpenseDate = date,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = VoidedAt(voided),
            VoidReason = voided ? "Ghi nhầm" : null,
        };

    private static Sale Sale(
        Guid seasonId,
        SalesChannel? channel,
        Guid? customerId,
        decimal quantity,
        decimal unitPrice,
        DateOnly date,
        bool voided = false) =>
        new()
        {
            SeasonId = seasonId,
            Channel = channel,
            CustomerId = customerId,
            QualityGrade = QualityGrade.Grade1,
            Quantity = quantity,
            UnitPrice = unitPrice,
            SaleDate = date,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = VoidedAt(voided),
            VoidReason = voided ? "Ghi nhầm" : null,
        };

    private static Payment Payment(Guid saleId, decimal amount, DateOnly date, bool voided = false) =>
        new()
        {
            SaleId = saleId,
            Amount = amount,
            PaymentDate = date,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = VoidedAt(voided),
            VoidReason = voided ? "Ghi nhầm" : null,
        };
}
