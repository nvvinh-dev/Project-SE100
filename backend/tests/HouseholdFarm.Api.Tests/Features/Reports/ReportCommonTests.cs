using System.Text.Json;
using ClosedXML.Excel;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Reports.Common;
using HouseholdFarm.Api.Tests.Infrastructure;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Tests.Features.Reports;

/// <summary>Kiểm tra phần dùng chung báo cáo: bộ lọc, phân quyền, tạm tính/hủy, xuất Excel và nhật ký (FR-RPT-01..04, FR-RPT-06).</summary>
public class ReportCommonTests(ApiFactory factory) : IClassFixture<ApiFactory>
{
    private static readonly DateTimeOffset _fixedNow = new(2026, 11, 13, 10, 15, 30, TimeSpan.Zero); // 17:15:30 VN

    [Fact(DisplayName = "FR-RPT-01: bộ lọc from > to và định dạng xuất thiếu/sai đều báo VALIDATION_ERROR")]
    public void Validators_RejectInvalidDateRangeAndFormat()
    {
        var filterValidator = new ReportFilterValidator();
        var invalidRange = filterValidator.Validate(new ReportFilter
        {
            From = new DateOnly(2026, 10, 20),
            To = new DateOnly(2026, 10, 10),
        });
        var validSameDay = filterValidator.Validate(new ReportFilter
        {
            From = new DateOnly(2026, 10, 10),
            To = new DateOnly(2026, 10, 10),
        });

        Assert.False(invalidRange.IsValid);
        Assert.Contains(invalidRange.Errors, e => e.PropertyName == nameof(ReportFilter.From) && e.ErrorMessage.Contains("Ngày bắt đầu"));
        Assert.True(validSameDay.IsValid);

        var exportValidator = new ExportReportRequestValidator();
        var missingFormat = exportValidator.Validate(new ExportReportRequest { Format = null });
        var invalidEnum = exportValidator.Validate(new ExportReportRequest { Format = (ExportFormat)999 });
        var validExcel = exportValidator.Validate(new ExportReportRequest { Format = ExportFormat.Excel });

        Assert.False(missingFormat.IsValid);
        Assert.Contains(missingFormat.Errors, e => e.PropertyName == nameof(ExportReportRequest.Format));
        Assert.False(invalidEnum.IsValid);
        Assert.True(validExcel.IsValid);
    }

    [Fact(DisplayName = "FR-RPT-02, BR-RPT-04: ma trận phân quyền xem báo cáo và ẩn cột tài chính khi thiếu quyền TC")]
    public async Task AccessService_EnforcesRoleAndExtraPermissionMatrix()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        var admin = await factory.CreateSystemAdminAsync(ct);
        var stockOnlyMember = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = household.Household.Id,
            Role = UserRole.Member,
            FullName = "Thành viên Kho",
            CanManageStock = true,
        };
        var financeOnlyMember = new ApplicationUser
        {
            Id = Guid.CreateVersion7(),
            HouseholdId = household.Household.Id,
            Role = UserRole.Member,
            FullName = "Thành viên TC",
            CanManageFinance = true,
        };

        // Chủ hộ: xem mọi báo cáo và thấy cột tài chính.
        var ownerAccess = new ReportAccessService(TestCurrentUser.For(household.Owner));
        ownerAccess.EnsureCanViewReport(ReportType.Production);
        ownerAccess.EnsureCanViewReport(ReportType.FarmTask);
        ownerAccess.EnsureCanViewReport(ReportType.Supply);
        ownerAccess.EnsureCanViewReport(ReportType.Finance);
        Assert.True(ownerAccess.CanViewFinancialColumns());

        // Thành viên thường: chỉ xem Sản xuất & Công việc, ẩn cột tài chính, bị chặn Vật tư & Tài chính.
        var memberAccess = new ReportAccessService(TestCurrentUser.For(household.Member));
        memberAccess.EnsureCanViewReport(ReportType.Production);
        memberAccess.EnsureCanViewReport(ReportType.FarmTask);
        Assert.False(memberAccess.CanViewFinancialColumns());
        Assert.Equal(ErrorCodes.Forbidden, Assert.Throws<AppException>(() => memberAccess.EnsureCanViewReport(ReportType.Supply)).Code);
        Assert.Equal(ErrorCodes.Forbidden, Assert.Throws<AppException>(() => memberAccess.EnsureCanViewReport(ReportType.Finance)).Code);

        // Thành viên chỉ có Kho: xem được Vật tư, bị chặn Tài chính và không thấy cột tài chính ở Sản xuất.
        var stockAccess = new ReportAccessService(TestCurrentUser.For(stockOnlyMember));
        stockAccess.EnsureCanViewReport(ReportType.Supply);
        Assert.False(stockAccess.CanViewFinancialColumns());
        Assert.Equal(ErrorCodes.Forbidden, Assert.Throws<AppException>(() => stockAccess.EnsureCanViewReport(ReportType.Finance)).Code);

        // Thành viên chỉ có TC: xem được Tài chính và thấy cột tài chính ở Sản xuất, bị chặn Vật tư.
        var financeAccess = new ReportAccessService(TestCurrentUser.For(financeOnlyMember));
        financeAccess.EnsureCanViewReport(ReportType.Finance);
        Assert.True(financeAccess.CanViewFinancialColumns());
        Assert.Equal(ErrorCodes.Forbidden, Assert.Throws<AppException>(() => financeAccess.EnsureCanViewReport(ReportType.Supply)).Code);

        // Nhân công và Quản trị hệ thống: bị chặn mọi loại báo cáo.
        var workerAccess = new ReportAccessService(TestCurrentUser.For(household.Worker));
        var adminAccess = new ReportAccessService(TestCurrentUser.For(admin));
        foreach (var type in Enum.GetValues<ReportType>())
        {
            Assert.Equal(ErrorCodes.Forbidden, Assert.Throws<AppException>(() => workerAccess.EnsureCanViewReport(type)).Code);
            Assert.Equal(ErrorCodes.Forbidden, Assert.Throws<AppException>(() => adminAccess.EnsureCanViewReport(type)).Code);
        }
        Assert.False(workerAccess.CanViewFinancialColumns());
        Assert.False(adminAccess.CanViewFinancialColumns());
    }

    [Fact(DisplayName = "FR-RPT-01, FR-RPT-03, BR-RPT-13: lọc mùa vụ theo ngày VN, phạm vi TV, tạm tính và tách Cancelled")]
    public async Task SeasonQuery_FiltersByBusinessDateVietnamTimeScopeAndCancelledSplit()
    {
        var ct = TestContext.Current.CancellationToken;
        var householdA = await factory.CreateHouseholdAsync(ct);
        var householdB = await factory.CreateHouseholdAsync(ct);

        var plot1 = await TestData.CreatePlotAsync(factory.Postgres, householdA, "TD001", ct, areaM2: 1200m);
        var plot2 = await TestData.CreatePlotAsync(factory.Postgres, householdA, "TD002", ct, areaM2: 800m);
        var cropRice = await TestData.CreateCropAsync(factory.Postgres, householdA, "Lúa OM5451", ct);
        var cropCorn = await TestData.CreateCropAsync(factory.Postgres, householdA, "Bắp nếp", ct);
        var otherPlot = await TestData.CreatePlotAsync(factory.Postgres, householdB, "TD999", ct);

        // 1) Đang canh tác từ tháng 9: dù bắt đầu trước From (02/10) vẫn phải khớp vì chưa kết thúc.
        var sInProgress = await CreateCustomSeasonAsync(
            householdA, plot1.Id, cropRice.Id, SeasonStatus.InProgress,
            new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1), ct);
        // 2) Đã chốt lúc 2026-10-01 17:30 UTC (= 2026-10-02 00:30 giờ VN) -> khớp From = 2026-10-02.
        var sClosedOnFromVn = await CreateCustomSeasonAsync(
            householdA, plot2.Id, cropCorn.Id, SeasonStatus.Closed,
            new DateOnly(2026, 8, 1), new DateOnly(2026, 10, 15), ct,
            closedAt: new DateTime(2026, 10, 1, 17, 30, 0, DateTimeKind.Utc));
        // 3) Đã chốt lúc 2026-10-01 16:30 UTC (= 2026-10-01 23:30 giờ VN) -> bị loại khi From = 2026-10-02.
        await CreateCustomSeasonAsync(
            householdA, plot1.Id, cropRice.Id, SeasonStatus.Closed,
            new DateOnly(2026, 7, 1), new DateOnly(2026, 9, 30), ct,
            closedAt: new DateTime(2026, 10, 1, 16, 30, 0, DateTimeKind.Utc));
        // 4) Đã hủy trong kỳ (StartDate <= To, CancelledAt >= From VN) -> khớp bộ lọc nhưng tách riêng khối Cancelled.
        var sCancelled = await CreateCustomSeasonAsync(
            householdA, plot2.Id, cropRice.Id, SeasonStatus.Cancelled,
            new DateOnly(2026, 10, 5), new DateOnly(2026, 12, 20), ct,
            cancelledAt: new DateTime(2026, 10, 6, 1, 0, 0, DateTimeKind.Utc));
        // 5) Lên kế hoạch nhưng StartDate > To (15/10) -> bị loại.
        await CreateCustomSeasonAsync(
            householdA, plot1.Id, cropCorn.Id, SeasonStatus.Planned,
            new DateOnly(2026, 10, 20), new DateOnly(2027, 1, 10), ct);
        // 6) Hộ B -> không bao giờ xuất hiện (TC-10).
        await TestData.CreateSeasonAsync(factory.Postgres, householdB, otherPlot, SeasonStatus.InProgress, ct);

        await TestData.AssignMemberAsync(factory.Postgres, householdA, sInProgress, householdA.Member, ct);

        var timeZone = ReportSeasonQueryExtensions.GetAppTimeZone(factory.Services.GetRequiredService<IConfiguration>());
        var filter = new ReportFilter { From = new DateOnly(2026, 10, 2), To = new DateOnly(2026, 10, 15) };

        // Chủ hộ lọc theo From/To -> ra đúng 3 mùa vụ của hộ A, tách 2 thường + 1 Cancelled, đúng IsProvisional.
        await using var ownerDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(householdA.Owner));
        var ownerSeasons = await ownerDb.Seasons
            .AsNoTracking()
            .Include(s => s.Plot)
            .Include(s => s.Crop)
            .VisibleToReport(TestCurrentUser.For(householdA.Owner), ReportType.Production)
            .ApplyReportFilter(filter, timeZone)
            .OrderBy(s => s.StartDate)
            .ToListAsync(ct);

        var summaries = ownerSeasons.Select(s => s.ToSummaryDto()).ToList();
        var (active, cancelled) = summaries.PartitionCancelled(s => s.Status);

        Assert.Equal([sClosedOnFromVn.Id, sInProgress.Id], active.Select(s => s.SeasonId).ToArray());
        Assert.False(active.Single(s => s.SeasonId == sClosedOnFromVn.Id).IsProvisional);
        Assert.True(active.Single(s => s.SeasonId == sInProgress.Id).IsProvisional);

        var cancelledDto = Assert.Single(cancelled);
        Assert.Equal(sCancelled.Id, cancelledDto.SeasonId);
        Assert.False(cancelledDto.IsProvisional);

        // Lọc thêm theo CropId:
        var cornOnly = await ownerDb.Seasons
            .AsNoTracking()
            .VisibleToReport(TestCurrentUser.For(householdA.Owner), ReportType.Production)
            .ApplyReportFilter(filter with { CropId = cropCorn.Id }, timeZone)
            .Select(s => s.Id)
            .ToListAsync(ct);
        Assert.Equal([sClosedOnFromVn.Id], cornOnly);

        // Thành viên hộ chỉ được thấy mùa vụ được phân công (PV).
        var memberCurrent = TestCurrentUser.For(householdA.Member, [sInProgress.Id]);
        await using var memberDb = factory.Postgres.CreateDbContext(memberCurrent);
        var memberIds = await memberDb.Seasons
            .AsNoTracking()
            .VisibleToReport(memberCurrent, ReportType.Production)
            .ApplyReportFilter(filter, timeZone)
            .Select(s => s.Id)
            .ToListAsync(ct);
        Assert.Equal([sInProgress.Id], memberIds);
    }

    [Fact(DisplayName = "FR-RPT-04, FR-RPT-06, BR-RPT-09: xuất Excel đúng nội dung, định dạng và ghi report_exports + nhật ký Report.Export")]
    public async Task ExportExcel_GeneratesValidWorkbookAndPersistsHistoryAndAuditLog()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);
        await using var scope = CreateExportScope(household.Owner);

        var filter = new ReportFilter
        {
            From = new DateOnly(2026, 10, 1),
            To = new DateOnly(2026, 11, 13),
        };
        var document = new ReportExportDocument(
            Title: "Báo cáo sản xuất",
            FileNamePrefix: "bao-cao-san-xuat",
            FiltersSummary:
            [
                new("Khoảng thời gian", "01/10/2026 - 13/11/2026"),
            ],
            OverviewItems:
            [
                new("Tổng diện tích gieo trồng", "1.500 m²"),
            ],
            Sections:
            [
                new(
                    Title: "Danh sách mùa vụ",
                    Columns:
                    [
                        new("Mùa vụ", ReportColumnType.Text),
                        new("Ngày bắt đầu", ReportColumnType.Date),
                        new("Sản lượng thực tế", ReportColumnType.Decimal),
                        new("Độ lệch", ReportColumnType.Percent),
                        new("Lợi nhuận", ReportColumnType.Money),
                        new("Số công việc", ReportColumnType.Integer),
                    ],
                    Rows:
                    [
                        ["Lúa OM5451 · TD001", new DateOnly(2026, 10, 1), 5250.5m, 5.0m, 12500000m, 8],
                    ],
                    FooterNote: "Số liệu mùa vụ chưa chốt là tạm tính."),
            ],
            SheetName: "Sản xuất");

        var result = await scope.Service.ExportAsync(
            ReportType.Production,
            ExportFormat.Excel,
            filter,
            new { from = filter.From, to = filter.To, deviationThreshold = 20 },
            document,
            ct);

        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.ContentType);
        Assert.Equal("bao-cao-san-xuat-20261113-171530.xlsx", result.FileName);

        using var stream = new MemoryStream(result.Content);
        using var workbook = new XLWorkbook(stream);
        var sheet = workbook.Worksheet("Sản xuất");
        Assert.Equal("Báo cáo sản xuất", sheet.Cell(1, 1).GetString());
        Assert.Equal("13/11/2026 17:15", sheet.Cell(2, 2).GetString());

        // Tìm dòng dữ liệu của bảng để kiểm tra từng kiểu dữ liệu và format trong Excel.
        var dataRow = sheet.RowsUsed().Single(r => r.Cell(1).GetString() == "Lúa OM5451 · TD001");
        Assert.Equal("dd/MM/yyyy", dataRow.Cell(2).Style.NumberFormat.Format);
        Assert.Equal(5250.5d, dataRow.Cell(3).GetDouble(), 3);
        Assert.Equal(5.0d, dataRow.Cell(4).GetDouble(), 1);
        Assert.Equal("0.0\"%\"", dataRow.Cell(4).Style.NumberFormat.Format);
        Assert.Equal(12500000d, dataRow.Cell(5).GetDouble(), 0);
        Assert.Equal(8d, dataRow.Cell(6).GetDouble(), 0);

        await using var verifyDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        var exportRow = await verifyDb.ReportExports.SingleAsync(r => r.HouseholdId == household.Household.Id, ct);
        Assert.Equal(ReportType.Production, exportRow.ReportType);
        Assert.Equal(ExportFormat.Excel, exportRow.Format);
        Assert.Equal(filter.From, exportRow.FromDate);
        Assert.Equal(filter.To, exportRow.ToDate);
        Assert.Equal(household.Owner.Id, exportRow.ExportedById);
        Assert.Equal(_fixedNow.UtcDateTime, exportRow.ExportedAt);

        using var filtersDoc = JsonDocument.Parse(exportRow.Filters);
        Assert.Equal("2026-10-01", filtersDoc.RootElement.GetProperty("from").GetString());
        Assert.Equal(20, filtersDoc.RootElement.GetProperty("deviationThreshold").GetInt32());

        var auditRow = await verifyDb.AuditLogs
            .SingleAsync(a => a.HouseholdId == household.Household.Id && a.Action == AuditActions.ReportExport, ct);
        Assert.Equal("Report", auditRow.EntityType);
        Assert.Equal(exportRow.Id, auditRow.EntityId);
        Assert.Equal(household.Owner.Id, auditRow.ActorId);
        Assert.Equal(_fixedNow.UtcDateTime, auditRow.OccurredAt);
    }

    [Fact(DisplayName = "FR-RPT-02, FR-RPT-06: không đủ quyền hoặc lỗi định dạng thì từ chối và không lưu report_exports")]
    public async Task Export_UnauthorizedOrUnsupportedFormat_DoesNotPersistRecords()
    {
        var ct = TestContext.Current.CancellationToken;
        var household = await factory.CreateHouseholdAsync(ct);

        var filter = new ReportFilter();
        var document = new ReportExportDocument("Báo cáo", "bao-cao", [], [], []);

        await using (var memberScope = CreateExportScope(household.Member))
        {
            var forbidden = await Assert.ThrowsAsync<AppException>(() =>
                memberScope.Service.ExportAsync(ReportType.Finance, ExportFormat.Excel, filter, filter, document, ct));
            Assert.Equal(ErrorCodes.Forbidden, forbidden.Code);
        }

        await using (var ownerScope = CreateExportScope(household.Owner))
        {
            var unsupported = await Assert.ThrowsAsync<AppException>(() =>
                ownerScope.Service.ExportAsync(ReportType.Production, ExportFormat.Pdf, filter, filter, document, ct));
            Assert.Equal(ErrorCodes.ValidationError, unsupported.Code);
        }

        await using var verifyDb = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        Assert.Empty(await verifyDb.ReportExports.Where(r => r.HouseholdId == household.Household.Id).ToListAsync(ct));
        Assert.Empty(await verifyDb.AuditLogs.Where(a => a.HouseholdId == household.Household.Id && a.Action == AuditActions.ReportExport).ToListAsync(ct));
    }

    private async Task<Season> CreateCustomSeasonAsync(
        TestHousehold household,
        Guid plotId,
        Guid cropId,
        SeasonStatus status,
        DateOnly startDate,
        DateOnly expectedHarvestDate,
        CancellationToken ct,
        DateTime? closedAt = null,
        DateTime? cancelledAt = null)
    {
        var season = new Season
        {
            PlotId = plotId,
            CropId = cropId,
            StartDate = startDate,
            ExpectedHarvestDate = expectedHarvestDate,
            TargetYield = 5000m,
            Status = status,
            ClosedAt = closedAt,
            CancelledAt = cancelledAt,
            CancelReason = status == SeasonStatus.Cancelled ? "Hủy thử nghiệm" : null,
        };

        await using var db = factory.Postgres.CreateDbContext(TestCurrentUser.For(household.Owner));
        db.Seasons.Add(season);
        await db.SaveChangesAsync(ct);
        return season;
    }

    private ReportExportScope CreateExportScope(ApplicationUser user)
    {
        var currentUser = TestCurrentUser.For(user);
        var db = factory.Postgres.CreateDbContext(currentUser);
        var clock = TestClock.At(_fixedNow);
        var config = factory.Services.GetRequiredService<IConfiguration>();
        var jsonOptions = factory.Services.GetRequiredService<IOptions<JsonOptions>>();
        var audit = new AuditLogger(db, currentUser, clock, jsonOptions);
        var access = new ReportAccessService(currentUser);
        var service = new ReportExportService(db, currentUser, clock, config, audit, access, new ExcelReportWriter(), jsonOptions);
        return new ReportExportScope(db, service);
    }

    private sealed class ReportExportScope(AppDbContext db, ReportExportService service) : IAsyncDisposable
    {
        public ReportExportService Service => service;

        public ValueTask DisposeAsync() => db.DisposeAsync();
    }
}
