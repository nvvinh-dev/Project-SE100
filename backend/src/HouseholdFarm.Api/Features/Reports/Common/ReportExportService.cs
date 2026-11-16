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

public sealed class ReportExportService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    IConfiguration configuration,
    IAuditLogger audit,
    IReportAccessService access,
    ExcelReportWriter excelWriter,
    PdfReportWriter pdfWriter,
    IOptions<JsonOptions> jsonOptions) : IReportExportService
{
    private const string ExcelContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string PdfContentType = "application/pdf";

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

        // Tạo tệp trước khi mở giao dịch để lỗi dựng tệp không ghi bản ghi rỗng xuống database.
        var exportedAt = clock.UtcNow;
        var (content, contentType, extension) = format switch
        {
            ExportFormat.Excel => (excelWriter.Write(document, exportedAt, _timeZone), ExcelContentType, "xlsx"),
            ExportFormat.Pdf => (pdfWriter.Write(document, exportedAt, _timeZone), PdfContentType, "pdf"),
            _ => throw new AppException(ErrorCodes.ValidationError, "Định dạng xuất báo cáo không được hỗ trợ."),
        };
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
        var fileName = $"{document.FileNamePrefix}-{exportedLocal.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.{extension}";
        return new ExportedFileResult(content, contentType, fileName);
    }
}
