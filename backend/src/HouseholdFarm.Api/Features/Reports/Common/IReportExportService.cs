using System.Globalization;
using System.Text.Json;
using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Dịch vụ xuất báo cáo dùng chung: kiểm tra quyền, tạo tệp Excel/PDF, lưu <c>report_exports</c> và ghi nhật ký
/// <c>Report.Export</c> trong cùng giao dịch database (FR-RPT-04, FR-RPT-06, BR-RPT-09).
/// </summary>
public interface IReportExportService
{
    Task<ExportedFileResult> ExportAsync(
        ReportType reportType,
        ExportFormat format,
        ReportFilter filter,
        object fullFilterPayload,
        ReportExportDocument document,
        CancellationToken ct);
}

public sealed class ReportExportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IConfiguration configuration,
    IAuditLogger audit,
    IReportAccessService access,
    ExcelReportWriter excelWriter,
    IOptions<JsonOptions> jsonOptions) : IReportExportService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private readonly JsonSerializerOptions _jsonOptions = jsonOptions.Value.SerializerOptions;
    private readonly TimeZoneInfo _timeZone = ReportSeasonQueryExtensions.GetAppTimeZone(configuration);

    public async Task<ExportedFileResult> ExportAsync(
        ReportType reportType,
        ExportFormat format,
        ReportFilter filter,
        object fullFilterPayload,
        ReportExportDocument document,
        CancellationToken ct)
    {
        access.EnsureCanViewReport(reportType);

        if (format != ExportFormat.Excel)
        {
            throw new AppException(ErrorCodes.ValidationError, "Hiện chỉ hỗ trợ xuất báo cáo định dạng Excel.");
        }

        // Tạo tệp trước khi mở giao dịch để lỗi dựng tệp không ghi bản ghi rỗng xuống database.
        var exportedAt = clock.UtcNow;
        var content = excelWriter.Write(document, exportedAt, _timeZone);
        var serializedFilters = JsonSerializer.Serialize(fullFilterPayload, _jsonOptions);

        // BR-RPT-09: mỗi lần xuất lưu 1 dòng report_exports và 1 dòng nhật ký Report.Export trong cùng transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(ct);

        var export = new ReportExport
        {
            ReportType = reportType,
            Format = format,
            FromDate = filter.From,
            ToDate = filter.To,
            Filters = serializedFilters,
            ExportedById = currentUser.UserId,
            ExportedAt = exportedAt,
        };
        db.ReportExports.Add(export);

        audit.Log(
            AuditActions.ReportExport,
            export.Id,
            oldValues: null,
            newValues: new
            {
                reportType,
                format,
                filter = fullFilterPayload,
            },
            reason: null);

        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        var exportedLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(exportedAt, DateTimeKind.Utc), _timeZone);
        var fileName = $"{document.FileNamePrefix}-{exportedLocal.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.xlsx";
        return new ExportedFileResult(content, ExcelContentType, fileName);
    }
}
