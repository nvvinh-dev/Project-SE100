using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Tests.Features.FarmTasks;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Tests.Features.Reports;

/// <summary>
/// Báo cáo vật tư (FR-RPT-SUP-01, FR-RPT-SUP-02, FR-RPT-SUP-03, BR-RPT-01, BR-RPT-05, BR-RPT-06, BR-SUP-12).
/// Kỳ lọc 01/10/2026 đến 31/10/2026.
/// </summary>
public class SupplyReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string SupplyReportPath = "/api/reports/supplies";
    private const string Period = "from=2026-10-01&to=2026-10-31";

    [Fact(DisplayName = "FR-RPT-SUP-01, BR-RPT-05: số liệu theo ví dụ giá vốn trong một kỳ, nhóm chỉ cộng giá trị")]
    public async Task Get_ComputesStockFiguresOfCostingExample()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeStockAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        // Ví dụ giá vốn: nhập 10 × 20.000, 10 × 24.000, xuất 15 × 22.000, nhập 5 × 30.000; một lần nhập bị Void không tính.
        var fertilizerGroup = Group(report, "Fertilizer");
        var fertilizer = Assert.Single(fertilizerGroup.GetProperty("supplies").EnumerateArray());
        Assert.Equal(0m, fertilizer.GetProperty("openingQuantity").GetDecimal());
        Assert.Equal(25m, fertilizer.GetProperty("inQuantity").GetDecimal());
        Assert.Equal(590_000m, fertilizer.GetProperty("inValue").GetDecimal());
        Assert.Equal(15m, fertilizer.GetProperty("outQuantity").GetDecimal());
        Assert.Equal(330_000m, fertilizer.GetProperty("outValue").GetDecimal());
        Assert.Equal(10m, fertilizer.GetProperty("closingQuantity").GetDecimal());
        Assert.Equal(10m, fertilizer.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(26_000m, fertilizer.GetProperty("averageUnitCost").GetDecimal());
        Assert.Equal(260_000m, fertilizer.GetProperty("stockValue").GetDecimal());

        Assert.Equal(590_000m, fertilizerGroup.GetProperty("inValue").GetDecimal());
        Assert.Equal(330_000m, fertilizerGroup.GetProperty("outValue").GetDecimal());
        Assert.Equal(260_000m, fertilizerGroup.GetProperty("stockValue").GetDecimal());
        Assert.False(fertilizerGroup.TryGetProperty("inQuantity", out _));
    }

    [Fact(DisplayName = "BR-RPT-05: giao dịch ngoài kỳ không tính vào nhập, xuất; trước kỳ vào tồn đầu kỳ, sau kỳ chỉ vào tồn hiện tại")]
    public async Task Get_TransactionsOutsidePeriod_GoToOpeningOrCurrentOnly()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeStockAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        // Giống: nhập 8 bao ngày 20/09 (trước kỳ), điều chỉnh −2 ngày 15/10, xuất 3 ngày 05/11 (sau kỳ).
        var seedGroup = Group(report, "Seed");
        var seed = Assert.Single(seedGroup.GetProperty("supplies").EnumerateArray());
        Assert.Equal(8m, seed.GetProperty("openingQuantity").GetDecimal());
        Assert.Equal(0m, seed.GetProperty("inQuantity").GetDecimal());
        Assert.Equal(0m, seed.GetProperty("outQuantity").GetDecimal());
        Assert.Equal(-2m, seed.GetProperty("adjustmentQuantity").GetDecimal());
        Assert.Equal(-10_000m, seed.GetProperty("adjustmentValue").GetDecimal());
        Assert.Equal(6m, seed.GetProperty("closingQuantity").GetDecimal());
        Assert.Equal(3m, seed.GetProperty("stockQuantity").GetDecimal());
        Assert.Equal(15_000m, seed.GetProperty("stockValue").GetDecimal());
        Assert.Equal(-10_000m, seedGroup.GetProperty("adjustmentValue").GetDecimal());
        Assert.Equal(275_000m - 5_000m, report.GetProperty("totalStockValue").GetDecimal());
    }

    [Fact(DisplayName = "BR-RPT-05: mọi vật tư có tồn đầu kỳ + nhập − xuất + điều chỉnh = tồn cuối kỳ")]
    public async Task Get_OpeningPlusMovementsEqualsClosing()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeStockAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        var rows = report.GetProperty("groups").EnumerateArray()
            .SelectMany(g => g.GetProperty("supplies").EnumerateArray())
            .ToList();
        Assert.NotEmpty(rows);
        foreach (var row in rows)
        {
            Assert.Equal(
                row.GetProperty("closingQuantity").GetDecimal(),
                row.GetProperty("openingQuantity").GetDecimal()
                    + row.GetProperty("inQuantity").GetDecimal()
                    - row.GetProperty("outQuantity").GetDecimal()
                    + row.GetProperty("adjustmentQuantity").GetDecimal());
        }
    }

    [Fact(DisplayName = "FR-RPT-SUP-02, BR-SUP-12: vật tư dưới ngưỡng ở phần đầu, tồn âm xếp trước; đủ tồn, ngừng sử dụng không có")]
    public async Task Get_ListsBelowMinSuppliesFirst()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeStockAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        var lowStock = report.GetProperty("lowStock").EnumerateArray()
            .Select(i => (i.GetProperty("supplyId").GetGuid(), i.GetProperty("stockQuantity").GetDecimal(),
                i.GetProperty("minStockLevel").GetDecimal()))
            .ToList();
        // Thuốc tồn −1 đứng đầu, giống tồn 3 < ngưỡng 5; phân tồn 10 bằng ngưỡng và vật tư ngừng sử dụng không có.
        Assert.Equal([(data.Pesticide, -1m, 2m), (data.Seed, 3m, 5m)], lowStock);
        Assert.Equal("lowStock", report.EnumerateObject().First().Name);
    }

    [Fact(DisplayName = "FR-RPT-01: lọc theo nhóm vật tư thì chỉ có vật tư của nhóm đó ở mọi phần")]
    public async Task Get_FilterByGroup()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeStockAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, $"{Period}&group=Seed", ct);

        Assert.Equal("Seed", Assert.Single(report.GetProperty("groups").EnumerateArray()).GetProperty("group").GetString());
        Assert.Equal(data.Seed, Assert.Single(report.GetProperty("lowStock").EnumerateArray()).GetProperty("supplyId").GetGuid());
        Assert.Empty(report.GetProperty("reconciliation").EnumerateArray());
        Assert.Equal(15_000m, report.GetProperty("totalStockValue").GetDecimal());
    }

    [Fact(DisplayName = "FR-RPT-SUP-03, BR-RPT-06: chỉ có xuất từ công việc thì chênh lệch bằng 0")]
    public async Task Reconciliation_OnlyTaskOuts_HasZeroDifference()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeReconciliationAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        var item = Reconciliation(report, data.Seed, data.SeasonB);
        Assert.Equal(5m, item.GetProperty("totalOutQuantity").GetDecimal());
        Assert.Equal(5m, item.GetProperty("taskOutQuantity").GetDecimal());
        Assert.Equal(5m, item.GetProperty("recordedInTasksQuantity").GetDecimal());
        Assert.Equal(0m, item.GetProperty("differenceQuantity").GetDecimal());
    }

    [Fact(DisplayName = "FR-RPT-SUP-03, BR-RPT-06: có xuất thủ công thì chênh lệch bằng lượng xuất thủ công, lần xuất nằm ở danh sách riêng")]
    public async Task Reconciliation_WithManualOut_ShowsDifferenceAndListsManualOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeReconciliationAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        // Xuất từ công việc 15, xuất thủ công 2; giao dịch xuất 4 của công việc đã Void không tính.
        var item = Reconciliation(report, data.Fertilizer, data.SeasonA);
        Assert.Equal(17m, item.GetProperty("totalOutQuantity").GetDecimal());
        Assert.Equal(15m, item.GetProperty("taskOutQuantity").GetDecimal());
        Assert.Equal(2m, item.GetProperty("manualOutQuantity").GetDecimal());
        Assert.Equal(15m, item.GetProperty("recordedInTasksQuantity").GetDecimal());
        Assert.Equal(2m, item.GetProperty("differenceQuantity").GetDecimal());

        // Lần xuất thủ công ngoài kỳ không có trong danh sách.
        var manual = Assert.Single(report.GetProperty("manualOuts").EnumerateArray());
        Assert.Equal(data.ManualOutId, manual.GetProperty("stockTransactionId").GetGuid());
        Assert.Equal("2026-10-12", manual.GetProperty("transactionDate").GetString());
        Assert.Equal(2m, manual.GetProperty("quantity").GetDecimal());
        Assert.Equal(data.SeasonA, manual.GetProperty("season").GetProperty("seasonId").GetGuid());
        Assert.Equal(data.Household.MemberWithAllPermissions.FullName, manual.GetProperty("createdByName").GetString());
    }

    [Fact(DisplayName = "BR-RPT-01: giao dịch xuất của công việc bị Void không tính vào đối chiếu")]
    public async Task Reconciliation_IgnoresVoidedTaskOut()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeReconciliationAsync(ct);

        var report = await GetReportAsync(data.Household.Owner, Period, ct);

        // Mùa C chỉ có giao dịch xuất của công việc đã Void nên không có dòng đối chiếu.
        Assert.DoesNotContain(
            report.GetProperty("reconciliation").EnumerateArray(),
            i => i.GetProperty("season").GetProperty("seasonId").GetGuid() == data.SeasonC);
        Assert.Equal(2, report.GetProperty("reconciliation").GetArrayLength());
    }

    [Fact(DisplayName = "FR-RPT-02: Thành viên có Kho xem được; Thành viên không có Kho, Nhân công bị FORBIDDEN")]
    public async Task Get_ChecksStockPermission()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var allowed = await factory.CreateClientFor(household.MemberWithAllPermissions).GetAsync($"{SupplyReportPath}?{Period}", ct);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);

        foreach (var user in new[] { household.Member, household.Worker })
        {
            var response = await factory.CreateClientFor(user).GetAsync($"{SupplyReportPath}?{Period}", ct);
            await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        }
    }

    [Fact(DisplayName = "FR-RPT-01: ngày bắt đầu sau ngày kết thúc thì VALIDATION_ERROR")]
    public async Task Get_FromAfterTo_ReturnsValidationError()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Owner)
            .GetAsync($"{SupplyReportPath}?from=2026-11-01&to=2026-10-01", ct);

        await response.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-RPT-04, FR-RPT-06, FR-RPT-SUP-02: xuất Excel có phần dưới ngưỡng đứng đầu, lưu report_exports và nhật ký")]
    public async Task Export_PutsLowStockFirstAndRecordsExport()
    {
        var ct = TestContext.Current.CancellationToken;
        var data = await ArrangeStockAsync(ct);
        var client = factory.CreateClientFor(data.Household.Owner);

        var response = await client.PostAsJsonAsync(
            $"{SupplyReportPath}/export",
            new { format = "Excel", from = "2026-10-01", to = "2026-10-31", group = (string?)null },
            ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.Equal("Báo cáo vật tư", sheet.Cell(1, 1).GetString());
        var lowStockRow = sheet.CellsUsed().First(c => c.GetString() == "Vật tư dưới ngưỡng" && c.Address.ColumnNumber == 1).Address.RowNumber;
        var stockRow = sheet.CellsUsed().First(c => c.GetString().StartsWith("Nhập, xuất, tồn:", StringComparison.Ordinal)).Address.RowNumber;
        Assert.True(lowStockRow < stockRow);
        Assert.Contains(sheet.CellsUsed(), c => c.GetString() == "Đối chiếu xuất kho với vật tư ghi trong công việc");

        await using var db = factory.Postgres.CreateDbContext();
        var export = await db.ReportExports.IgnoreQueryFilters()
            .SingleAsync(r => r.HouseholdId == data.Household.Household.Id, ct);
        Assert.Equal(ReportType.Supply, export.ReportType);
        Assert.Equal(new DateOnly(2026, 10, 1), export.FromDate);
        Assert.Equal(new DateOnly(2026, 10, 31), export.ToDate);
        await AuditLogAssertions.AssertSingleAsync(
            factory.Postgres, data.Household.Household.Id, AuditActions.ReportExport, export.Id, ct);
    }

    [Fact(DisplayName = "FR-RPT-02: Thành viên không có Kho xuất báo cáo vật tư bị FORBIDDEN, không lưu lần xuất")]
    public async Task Export_WithoutStockPermission_ReturnsForbidden()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var response = await factory.CreateClientFor(household.Member)
            .PostAsJsonAsync($"{SupplyReportPath}/export", new { format = "Pdf" }, ct);

        await response.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
        await using var db = factory.Postgres.CreateDbContext();
        Assert.False(await db.ReportExports.IgnoreQueryFilters().AnyAsync(r => r.HouseholdId == household.Household.Id, ct));
    }

    private sealed record StockScenario(TestHousehold Household, Guid Fertilizer, Guid Seed, Guid Pesticide);

    private sealed record ReconciliationScenario(
        TestHousehold Household,
        Guid Fertilizer,
        Guid Seed,
        Guid SeasonA,
        Guid SeasonB,
        Guid SeasonC,
        Guid ManualOutId);

    private async Task<JsonElement> GetReportAsync(ApplicationUser user, string queryString, CancellationToken ct)
    {
        var response = await factory.CreateClientFor(user).GetAsync($"{SupplyReportPath}?{queryString}", ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data");
    }

    private static JsonElement Group(JsonElement report, string group) =>
        report.GetProperty("groups").EnumerateArray().Single(g => g.GetProperty("group").GetString() == group);

    private static JsonElement Reconciliation(JsonElement report, Guid supplyId, Guid seasonId) =>
        report.GetProperty("reconciliation").EnumerateArray().Single(i =>
            i.GetProperty("supplyId").GetGuid() == supplyId
            && i.GetProperty("season").GetProperty("seasonId").GetGuid() == seasonId);

    private async Task<StockScenario> ArrangeStockAsync(CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var season = await TestData.CreateSeasonAsync(factory.Postgres, household, plot, SeasonStatus.InProgress, ct);
        var fertilizer = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, group: SupplyGroup.Fertilizer, minStockLevel: 10m, averageUnitCost: 26_000m);
        var seed = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT002", ct, group: SupplyGroup.Seed, unit: Unit.Bag, minStockLevel: 5m, averageUnitCost: 5_000m);
        var pesticide = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT003", ct, group: SupplyGroup.Pesticide, unit: Unit.Bottle, minStockLevel: 2m,
            averageUnitCost: 5_000m);
        await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT004", ct, group: SupplyGroup.Pesticide, minStockLevel: 10m, status: RecordStatus.Inactive);

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.StockTransactions.AddRange(
            Stock(fertilizer, StockTransactionType.In, 10m, 20_000m, new DateOnly(2026, 10, 1)),
            Stock(fertilizer, StockTransactionType.In, 10m, 24_000m, new DateOnly(2026, 10, 2)),
            Stock(fertilizer, StockTransactionType.Out, -15m, 22_000m, new DateOnly(2026, 10, 5), season.Id),
            Stock(fertilizer, StockTransactionType.In, 5m, 30_000m, new DateOnly(2026, 10, 6)),
            Stock(fertilizer, StockTransactionType.In, 7m, 99_000m, new DateOnly(2026, 10, 7), voided: true),
            Stock(seed, StockTransactionType.In, 8m, 5_000m, new DateOnly(2026, 9, 20)),
            Stock(seed, StockTransactionType.Adjustment, -2m, 5_000m, new DateOnly(2026, 10, 15)),
            Stock(seed, StockTransactionType.Out, -3m, 5_000m, new DateOnly(2026, 11, 5), season.Id),
            Stock(pesticide, StockTransactionType.Out, -1m, 5_000m, new DateOnly(2026, 10, 20), season.Id));
        await db.SaveChangesAsync(ct);

        return new StockScenario(household, fertilizer.Id, seed.Id, pesticide.Id);
    }

    private async Task<ReconciliationScenario> ArrangeReconciliationAsync(CancellationToken ct)
    {
        var household = await factory.CreateHouseholdAsync(ct);
        var plotA = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        var plotB = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct);
        var plotC = await TestData.CreatePlotAsync(factory.Postgres, household, "TD003", ct);
        var seasonA = await TestData.CreateSeasonAsync(factory.Postgres, household, plotA, SeasonStatus.InProgress, ct);
        var seasonB = await TestData.CreateSeasonAsync(factory.Postgres, household, plotB, SeasonStatus.InProgress, ct);
        var seasonC = await TestData.CreateSeasonAsync(factory.Postgres, household, plotC, SeasonStatus.InProgress, ct);
        var fertilizer = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, group: SupplyGroup.Fertilizer, averageUnitCost: 22_000m);
        var seed = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT002", ct, group: SupplyGroup.Seed, averageUnitCost: 5_000m);

        var taskOut = Stock(fertilizer, StockTransactionType.Out, -15m, 22_000m, new DateOnly(2026, 10, 5), seasonA.Id);
        var seedTaskOut = Stock(seed, StockTransactionType.Out, -5m, 5_000m, new DateOnly(2026, 10, 8), seasonB.Id);
        var voidedTaskOut = Stock(fertilizer, StockTransactionType.Out, -4m, 22_000m, new DateOnly(2026, 10, 9), seasonC.Id, voided: true);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.StockTransactions.AddRange(
                Stock(fertilizer, StockTransactionType.In, 50m, 22_000m, new DateOnly(2026, 9, 1)),
                Stock(seed, StockTransactionType.In, 50m, 5_000m, new DateOnly(2026, 9, 1)),
                taskOut,
                seedTaskOut,
                voidedTaskOut,
                Stock(fertilizer, StockTransactionType.Out, -3m, 22_000m, new DateOnly(2026, 11, 2), seasonA.Id));
            await db.SaveChangesAsync(ct);
        }

        // Lần xuất thủ công do Thành viên có quyền Kho ghi.
        var manualOut = Stock(fertilizer, StockTransactionType.Out, -2m, 22_000m, new DateOnly(2026, 10, 12), seasonA.Id);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.MemberWithAllPermissions)))
        {
            db.StockTransactions.Add(manualOut);
            await db.SaveChangesAsync(ct);
        }

        await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, seasonA, new DateOnly(2026, 10, 5), ct,
            type: FarmTaskType.Fertilizing, status: FarmTaskStatus.Done, supplies: (fertilizer, 15m, 15m, taskOut.Id));
        await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, seasonB, new DateOnly(2026, 10, 8), ct,
            type: FarmTaskType.Sowing, status: FarmTaskStatus.Done, supplies: (seed, 5m, 5m, seedTaskOut.Id));
        await FarmTaskTestData.CreateTaskAsync(
            factory.Postgres, household, seasonC, new DateOnly(2026, 10, 9), ct,
            type: FarmTaskType.Fertilizing, status: FarmTaskStatus.Done, supplies: (fertilizer, 4m, 4m, voidedTaskOut.Id));

        return new ReconciliationScenario(household, fertilizer.Id, seed.Id, seasonA.Id, seasonB.Id, seasonC.Id, manualOut.Id);
    }

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
            Reason = type == StockTransactionType.Adjustment ? "Kiểm kê" : null,
            RequestId = Guid.CreateVersion7(),
            VoidedAt = voided ? new DateTime(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc) : null,
            VoidReason = voided ? "Ghi nhầm" : null,
        };
}
