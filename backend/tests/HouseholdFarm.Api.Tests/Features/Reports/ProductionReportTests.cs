using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Production;
using HouseholdFarm.Api.Tests.Features.Supplies;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HouseholdFarm.Api.Tests.Features.Reports;

/// <summary>
/// Báo cáo sản xuất: tổng quan, sản lượng, năng suất, lệch mục tiêu, so với mùa trước và xuất tệp
/// (FR-RPT-PROD-01..04, FR-RPT-04..06, BR-RPT-03, BR-RPT-04, BR-RPT-10..12).
/// </summary>
public class ProductionReportTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private const string ProductionPath = "/api/reports/production";

    [Fact(DisplayName = "FR-RPT-PROD-01, BR-RPT-12: tổng quan 4 trạng thái, cộng diện tích khi cùng thửa và phạm vi thành viên")]
    public async Task Get_OverviewCountsStatusesAreaAndEnforcesScope()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, areaM2: 1200m);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct, areaM2: 800m);

        var sInProgress = await CreateSeasonAsync(household, plot1, null, SeasonStatus.InProgress, new DateOnly(2026, 9, 1), 5000m, ct, assignee: household.Member);
        var sClosed = await CreateSeasonAsync(household, plot1, null, SeasonStatus.Closed, new DateOnly(2026, 6, 1), 4000m, ct, closedAt: new DateTime(2026, 8, 25, 0, 0, 0, DateTimeKind.Utc));
        await CreateSeasonAsync(household, plot2, null, SeasonStatus.Planned, new DateOnly(2026, 11, 1), 3000m, ct);
        await CreateSeasonAsync(household, plot2, null, SeasonStatus.Cancelled, new DateOnly(2026, 5, 1), 3000m, ct, cancelledAt: new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc));

        // Chủ hộ thấy toàn bộ 4 mùa vụ; tổng diện tích = 1200 (InProgress) + 1200 (Closed) = 2400 m2.
        var ownerReport = await GetReportAsync(household.Owner, "", ct);
        var ownerOverview = ownerReport.GetProperty("overview");
        Assert.Equal(1, ownerOverview.GetProperty("inProgressCount").GetInt32());
        Assert.Equal(1, ownerOverview.GetProperty("closedCount").GetInt32());
        Assert.Equal(1, ownerOverview.GetProperty("plannedCount").GetInt32());
        Assert.Equal(1, ownerOverview.GetProperty("cancelledCount").GetInt32());
        Assert.Equal(2400m, ownerOverview.GetProperty("totalCultivatedAreaM2").GetDecimal());
        Assert.Equal(3, ownerReport.GetProperty("seasons").GetArrayLength());
        Assert.Single(ownerReport.GetProperty("cancelledSeasons").EnumerateArray());

        // Thành viên thường chỉ thấy mùa vụ được phân công (sInProgress).
        var memberReport = await GetReportAsync(household.Member, "", ct);
        var memberOverview = memberReport.GetProperty("overview");
        Assert.Equal(1, memberOverview.GetProperty("inProgressCount").GetInt32());
        Assert.Equal(0, memberOverview.GetProperty("closedCount").GetInt32());
        Assert.Equal(1200m, memberOverview.GetProperty("totalCultivatedAreaM2").GetDecimal());
        var memberSeason = Assert.Single(memberReport.GetProperty("seasons").EnumerateArray());
        Assert.Equal(sInProgress.Id, memberSeason.GetProperty("season").GetProperty("seasonId").GetGuid());
        Assert.NotEqual(sClosed.Id, memberSeason.GetProperty("season").GetProperty("seasonId").GetGuid());

        // Nhân công bị FORBIDDEN.
        var workerResp = await factory.CreateClientFor(household.Worker).GetAsync(ProductionPath, ct);
        await workerResp.AssertProblemAsync(HttpStatusCode.Forbidden, ErrorCodes.Forbidden, ct);
    }

    [Fact(DisplayName = "FR-RPT-PROD-02, BR-RPT-04: tính sản lượng, độ lệch, năng suất và ẩn cột tài chính khi thiếu quyền TC")]
    public async Task Get_ComputesYieldMetricsAndHidesFinanceWithoutPermission()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, areaM2: 2000m);
        var season = await CreateSeasonAsync(
            household, plot, null, SeasonStatus.Closed, new DateOnly(2026, 6, 1), 1000m, ct,
            closedAt: new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Utc),
            assignee: household.Member);

        var supply = await SupplyTestData.CreateSupplyAsync(
            factory.Postgres, household, "VT001", ct, name: "Phân NPK", group: SupplyGroup.Fertilizer, unit: Unit.Kilogram);
        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.HarvestRecords.AddRange(
                new HarvestRecord
                {
                    SeasonId = season.Id,
                    HarvestDate = new DateOnly(2026, 8, 20),
                    QualityGrade = QualityGrade.Grade1,
                    Quantity = 1250m,
                    RequestId = Guid.CreateVersion7(),
                },
                new HarvestRecord
                {
                    SeasonId = season.Id,
                    HarvestDate = new DateOnly(2026, 8, 21),
                    QualityGrade = QualityGrade.Grade1,
                    Quantity = 500m,
                    RequestId = Guid.CreateVersion7(),
                    VoidedAt = DateTime.UtcNow,
                    VoidReason = "Ghi nhầm",
                });
            db.StockTransactions.Add(new StockTransaction
            {
                SupplyId = supply.Id,
                Type = StockTransactionType.Out,
                TransactionDate = new DateOnly(2026, 7, 1),
                Quantity = -10m,
                UnitCost = 20_000m,
                TotalCost = 200_000m,
                SeasonId = season.Id,
                RequestId = Guid.CreateVersion7(),
            });
            db.Sales.Add(new Sale
            {
                SeasonId = season.Id,
                Channel = SalesChannel.Trader,
                QualityGrade = QualityGrade.Grade1,
                Quantity = 1000m,
                UnitPrice = 1000m,
                SaleDate = new DateOnly(2026, 8, 25),
                RequestId = Guid.CreateVersion7(),
            });
            await db.SaveChangesAsync(ct);
        }

        // Thành viên có TC (MemberWithAllPermissions) được phân công thêm hoặc Chủ hộ đều thấy cột tài chính.
        var ownerReport = await GetReportAsync(household.Owner, "", ct);
        Assert.True(ownerReport.GetProperty("includesFinancialColumns").GetBoolean());
        var ownerRow = Assert.Single(ownerReport.GetProperty("seasons").EnumerateArray());
        Assert.Equal(1250m, ownerRow.GetProperty("actualYield").GetDecimal());
        Assert.Equal(25.0m, ownerRow.GetProperty("yieldDeviation").GetDecimal());
        Assert.Equal(0.625m, ownerRow.GetProperty("yieldPerM2").GetDecimal());
        Assert.Equal(6250m, ownerRow.GetProperty("yieldPerHectare").GetDecimal());
        Assert.Equal(200_000m, ownerRow.GetProperty("supplyCost").GetDecimal());
        Assert.Equal(1_000_000m, ownerRow.GetProperty("revenue").GetDecimal());
        Assert.Equal(800_000m, ownerRow.GetProperty("profit").GetDecimal());

        // Thành viên thường (không có ManageFinance): các trường tài chính trả null.
        var memberReport = await GetReportAsync(household.Member, "", ct);
        Assert.False(memberReport.GetProperty("includesFinancialColumns").GetBoolean());
        var memberRow = Assert.Single(memberReport.GetProperty("seasons").EnumerateArray());
        Assert.Equal(1250m, memberRow.GetProperty("actualYield").GetDecimal());
        Assert.Equal(JsonValueKind.Null, memberRow.GetProperty("supplyCost").ValueKind);
        Assert.Equal(JsonValueKind.Null, memberRow.GetProperty("revenue").ValueKind);
        Assert.Equal(JsonValueKind.Null, memberRow.GetProperty("profit").ValueKind);

        // Xuất Excel cho Thành viên thường không được chứa cột tài chính.
        var exportResp = await factory.CreateClientFor(household.Member)
            .PostAsJsonAsync($"{ProductionPath}/export", new { format = "Excel" }, ct);
        Assert.Equal(HttpStatusCode.OK, exportResp.StatusCode);
        await using var stream = await exportResp.Content.ReadAsStreamAsync(ct);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet(1);
        Assert.DoesNotContain(sheet.CellsUsed(), c => c.GetString() is "Chi phí vật tư" or "Doanh thu" or "Lợi nhuận");
    }

    [Fact(DisplayName = "FR-RPT-PROD-03, BR-RPT-10: đánh dấu lệch nhiều chỉ với mùa Closed đạt ngưỡng, ngưỡng âm báo lỗi")]
    public async Task Get_HighDeviation_AppliesThresholdRulesAndRejectsNegative()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);

        var closedUnder = await CreateSeasonAsync(household, plot, null, SeasonStatus.Closed, new DateOnly(2026, 1, 1), 1000m, ct, closedAt: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        var closedBoundary = await CreateSeasonAsync(household, plot, null, SeasonStatus.Closed, new DateOnly(2026, 4, 1), 1000m, ct, closedAt: new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        var inProgressHuge = await CreateSeasonAsync(household, plot, null, SeasonStatus.InProgress, new DateOnly(2026, 9, 1), 1000m, ct);

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.HarvestRecords.AddRange(
                new HarvestRecord { SeasonId = closedUnder.Id, HarvestDate = new DateOnly(2026, 2, 20), QualityGrade = QualityGrade.Grade1, Quantity = 750m, RequestId = Guid.CreateVersion7() }, // -25%
                new HarvestRecord { SeasonId = closedBoundary.Id, HarvestDate = new DateOnly(2026, 5, 20), QualityGrade = QualityGrade.Grade1, Quantity = 1200m, RequestId = Guid.CreateVersion7() }, // +20%
                new HarvestRecord { SeasonId = inProgressHuge.Id, HarvestDate = new DateOnly(2026, 10, 1), QualityGrade = QualityGrade.Grade1, Quantity = 500m, RequestId = Guid.CreateVersion7() }); // -50% nhưng chưa Closed
            await db.SaveChangesAsync(ct);
        }

        var report = await GetReportAsync(household.Owner, "deviationThreshold=20", ct);
        var rows = report.GetProperty("seasons").EnumerateArray().ToList();
        Assert.True(rows.Single(r => SeasonId(r) == closedUnder.Id).GetProperty("isHighDeviation").GetBoolean());
        Assert.True(rows.Single(r => SeasonId(r) == closedBoundary.Id).GetProperty("isHighDeviation").GetBoolean());
        var activeRow = rows.Single(r => SeasonId(r) == inProgressHuge.Id);
        Assert.Equal(-50.0m, activeRow.GetProperty("yieldDeviation").GetDecimal());
        Assert.False(activeRow.GetProperty("isHighDeviation").GetBoolean());

        var filtered = await GetReportAsync(household.Owner, "deviationThreshold=20&onlyHighDeviation=true", ct);
        var filteredIds = filtered.GetProperty("seasons").EnumerateArray().Select(SeasonId).ToHashSet();
        Assert.Equal([closedUnder.Id, closedBoundary.Id], filteredIds);

        var invalidResp = await factory.CreateClientFor(household.Owner).GetAsync($"{ProductionPath}?deviationThreshold=-5", ct);
        await invalidResp.AssertProblemAsync(HttpStatusCode.BadRequest, ErrorCodes.ValidationError, ct);
    }

    [Fact(DisplayName = "FR-RPT-PROD-04, BR-RPT-11: chọn đúng mùa trước dùng chung, bỏ qua Cancelled, xử lý khác đơn vị và chưa có mùa trước")]
    public async Task Get_AndPreviousSeasonService_HandlesMatchCancelAndDifferentUnits()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct, areaM2: 1000m);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, household, "TD002", ct, areaM2: 2000m);

        var cropKg = await CreateCropAsync(household, "Lúa ST25", Unit.Kilogram, ct);
        var cropTon = await CreateCropAsync(household, "Bắp sinh khối", Unit.Ton, ct);

        // Plot 1 có: mùa 1 Closed (kg), mùa 2 Cancelled (kg) xen giữa, mùa 3 hiện tại InProgress (kg).
        var sOld = await CreateSeasonAsync(household, plot1, cropKg, SeasonStatus.Closed, new DateOnly(2026, 1, 1), 1000m, ct, closedAt: new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        await CreateSeasonAsync(household, plot1, cropKg, SeasonStatus.Cancelled, new DateOnly(2026, 4, 1), 1000m, ct, cancelledAt: new DateTime(2026, 4, 10, 0, 0, 0, DateTimeKind.Utc));
        var sCurrent = await CreateSeasonAsync(household, plot1, cropKg, SeasonStatus.InProgress, new DateOnly(2026, 7, 1), 1000m, ct);

        // Plot 2 có: mùa cũ Closed (Ton), mùa mới Closed (Kilogram) để kiểm tra thông điệp khác đơn vị.
        var sTonClosed = await CreateSeasonAsync(household, plot2, cropTon, SeasonStatus.Closed, new DateOnly(2026, 2, 1), 10m, ct, closedAt: new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
        var sKgAfterTon = await CreateSeasonAsync(household, plot2, cropKg, SeasonStatus.Closed, new DateOnly(2026, 6, 1), 2000m, ct, closedAt: new DateTime(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc));

        await using (var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner)))
        {
            db.HarvestRecords.AddRange(
                new HarvestRecord { SeasonId = sOld.Id, HarvestDate = new DateOnly(2026, 2, 20), QualityGrade = QualityGrade.Grade1, Quantity = 500m, RequestId = Guid.CreateVersion7() },
                new HarvestRecord { SeasonId = sCurrent.Id, HarvestDate = new DateOnly(2026, 9, 1), QualityGrade = QualityGrade.Grade1, Quantity = 700m, RequestId = Guid.CreateVersion7() },
                new HarvestRecord { SeasonId = sTonClosed.Id, HarvestDate = new DateOnly(2026, 3, 25), QualityGrade = QualityGrade.Grade1, Quantity = 8m, RequestId = Guid.CreateVersion7() },
                new HarvestRecord { SeasonId = sKgAfterTon.Id, HarvestDate = new DateOnly(2026, 7, 25), QualityGrade = QualityGrade.Grade1, Quantity = 1800m, RequestId = Guid.CreateVersion7() });
            await db.SaveChangesAsync(ct);
        }

        // Kiểm tra hàm dùng chung IPreviousSeasonService (cho AI dùng tiêu chí mặc định SamePlot).
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
            var prevService = new PreviousSeasonService(db);
            var matched = await prevService.FindPreviousSeasonAsync(sCurrent.Id, PreviousSeasonCompareBy.SamePlot, ct);
            Assert.NotNull(matched);
            Assert.Equal(sOld.Id, matched.SeasonId);
            Assert.Equal(5000m, matched.YieldPerHectare);
        }

        var report = await GetReportAsync(household.Owner, "compareBy=SamePlot", ct);
        var items = report.GetProperty("seasons").EnumerateArray().ToList();

        // sCurrent so với sOld (bỏ qua Cancelled).
        var currentComp = items.Single(i => SeasonId(i) == sCurrent.Id).GetProperty("previousComparison");
        Assert.Equal(sOld.Id, currentComp.GetProperty("previousSeasonId").GetGuid());
        Assert.True(currentComp.GetProperty("sameYieldUnit").GetBoolean());
        Assert.Equal(2000m, currentComp.GetProperty("yieldPerHectareDelta").GetDecimal());
        Assert.Equal(JsonValueKind.Null, currentComp.GetProperty("message").ValueKind);

        // sKgAfterTon trên Plot 2 so với sTonClosed -> khác đơn vị.
        var diffUnitComp = items.Single(i => SeasonId(i) == sKgAfterTon.Id).GetProperty("previousComparison");
        Assert.Equal(sTonClosed.Id, diffUnitComp.GetProperty("previousSeasonId").GetGuid());
        Assert.False(diffUnitComp.GetProperty("sameYieldUnit").GetBoolean());
        Assert.Equal(JsonValueKind.Null, diffUnitComp.GetProperty("yieldPerHectareDelta").ValueKind);
        Assert.Equal("Khác đơn vị, không so sánh", diffUnitComp.GetProperty("message").GetString());

        // sOld không có mùa trước.
        var oldComp = items.Single(i => SeasonId(i) == sOld.Id).GetProperty("previousComparison");
        Assert.Equal(JsonValueKind.Null, oldComp.GetProperty("previousSeasonId").ValueKind);
        Assert.Equal("Chưa có mùa trước để so sánh", oldComp.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "FR-RPT-04, FR-RPT-05, FR-RPT-06: xuất Excel và PDF báo cáo sản xuất, lưu lịch sử xuất và nhật ký")]
    public async Task Export_ExcelAndPdf_ReturnsFilesAndPersistsAudit()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var plot = await TestData.CreatePlotAsync(factory.Postgres, household, "TD001", ct);
        await CreateSeasonAsync(household, plot, null, SeasonStatus.InProgress, new DateOnly(2026, 9, 1), 1000m, ct);

        var client = factory.CreateClientFor(household.Owner);

        var excelResp = await client.PostAsJsonAsync($"{ProductionPath}/export", new { format = "Excel" }, ct);
        Assert.Equal(HttpStatusCode.OK, excelResp.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", excelResp.Content.Headers.ContentType?.MediaType);

        var pdfResp = await client.PostAsJsonAsync($"{ProductionPath}/export", new { format = "Pdf" }, ct);
        Assert.Equal(HttpStatusCode.OK, pdfResp.StatusCode);
        Assert.Equal("application/pdf", pdfResp.Content.Headers.ContentType?.MediaType);
        var pdfBytes = await pdfResp.Content.ReadAsByteArrayAsync(ct);
        Assert.StartsWith("%PDF-", System.Text.Encoding.ASCII.GetString(pdfBytes, 0, 5));

        await using var db = factory.Postgres.CreateDbContext();
        var exports = await db.ReportExports.IgnoreQueryFilters()
            .Where(r => r.HouseholdId == household.Household.Id)
            .ToListAsync(ct);
        Assert.Equal(2, exports.Count);
        Assert.Contains(exports, e => e.ReportType == ReportType.Production && e.Format == ExportFormat.Excel);
        Assert.Contains(exports, e => e.ReportType == ReportType.Production && e.Format == ExportFormat.Pdf);
        var auditCount = await db.AuditLogs.IgnoreQueryFilters()
            .CountAsync(a => a.HouseholdId == household.Household.Id && a.Action == AuditActions.ReportExport, ct);
        Assert.Equal(2, auditCount);
    }

    private async Task<JsonElement> GetReportAsync(ApplicationUser user, string query, CancellationToken ct)
    {
        var path = string.IsNullOrEmpty(query) ? ProductionPath : $"{ProductionPath}?{query}";
        var response = await factory.CreateClientFor(user).GetAsync(path, ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.ReadJsonAsync(ct)).GetProperty("data");
    }

    private static Guid SeasonId(JsonElement item) => item.GetProperty("season").GetProperty("seasonId").GetGuid();

    private async Task<Crop> CreateCropAsync(TestHousehold household, string name, Unit unit, CancellationToken ct)
    {
        var crop = new Crop
        {
            Name = name,
            Group = CropGroup.Staple,
            YieldUnit = unit,
            Status = RecordStatus.Active,
        };
        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Crops.Add(crop);
        await db.SaveChangesAsync(ct);
        return crop;
    }

    private async Task<Season> CreateSeasonAsync(
        TestHousehold household,
        Plot plot,
        Crop? crop,
        SeasonStatus status,
        DateOnly startDate,
        decimal targetYield,
        CancellationToken ct,
        DateTime? closedAt = null,
        DateTime? cancelledAt = null,
        ApplicationUser? assignee = null)
    {
        var actualCrop = crop ?? await CreateCropAsync(household, $"Cây {Guid.CreateVersion7():N}", Unit.Kilogram, ct);
        var season = new Season
        {
            PlotId = plot.Id,
            CropId = actualCrop.Id,
            StartDate = startDate,
            ExpectedHarvestDate = startDate.AddDays(90),
            TargetYield = targetYield,
            Status = status,
            ClosedAt = closedAt,
            CancelledAt = cancelledAt,
            CancelReason = status == SeasonStatus.Cancelled ? "Hủy thử nghiệm" : null,
        };

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Seasons.Add(season);
        if (assignee is not null)
        {
            db.SeasonAssignments.Add(new SeasonAssignment
            {
                SeasonId = season.Id,
                UserId = assignee.Id,
            });
        }
        await db.SaveChangesAsync(ct);
        return season;
    }
}
