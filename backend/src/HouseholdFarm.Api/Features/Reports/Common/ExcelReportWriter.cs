using System.Globalization;
using ClosedXML.Excel;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Tạo tệp Excel (.xlsx) từ <see cref="ReportExportDocument"/> (FR-RPT-04).
/// Tính độ rộng cột theo độ dài nội dung để không phụ thuộc font hệ thống trên Linux/CI.
/// </summary>
public sealed class ExcelReportWriter
{
    private static readonly char[] _invalidSheetChars = [':', '\\', '/', '?', '*', '[', ']'];

    public byte[] Write(ReportExportDocument document, DateTime exportedAtUtc, TimeZoneInfo timeZone)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(SanitizeSheetName(document.SheetName));
        var columnWidths = new Dictionary<int, int>();

        var row = 1;
        var titleCell = worksheet.Cell(row, 1);
        titleCell.Value = document.Title;
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 14;
        row++;

        var exportedLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(exportedAtUtc, DateTimeKind.Utc),
            timeZone);
        WriteSummaryPair(worksheet, row++, "Ngày xuất", exportedLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), columnWidths);

        if (document.FiltersSummary.Count > 0)
        {
            row++;
            row = WriteSummaryBlock(worksheet, row, "Bộ lọc", document.FiltersSummary, columnWidths);
        }

        if (document.OverviewItems.Count > 0)
        {
            row++;
            row = WriteSummaryBlock(worksheet, row, "Tổng quan", document.OverviewItems, columnWidths);
        }

        foreach (var section in document.Sections)
        {
            row++;
            row = WriteSection(worksheet, row, section, timeZone, columnWidths);
        }

        foreach (var (colIndex, maxLength) in columnWidths)
        {
            worksheet.Column(colIndex).Width = Math.Clamp(maxLength + 4, 12, 48);
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static int WriteSummaryBlock(
        IXLWorksheet worksheet,
        int startRow,
        string heading,
        IReadOnlyList<ReportSummaryItem> items,
        Dictionary<int, int> columnWidths)
    {
        var headingCell = worksheet.Cell(startRow, 1);
        headingCell.Value = heading;
        headingCell.Style.Font.Bold = true;
        TrackWidth(columnWidths, 1, heading.Length);
        var row = startRow + 1;

        foreach (var item in items)
        {
            WriteSummaryPair(worksheet, row++, item.Label, item.Value, columnWidths);
        }

        return row;
    }

    private static void WriteSummaryPair(
        IXLWorksheet worksheet,
        int row,
        string label,
        string value,
        Dictionary<int, int> columnWidths)
    {
        var labelCell = worksheet.Cell(row, 1);
        labelCell.Value = label;
        labelCell.Style.Font.Bold = true;

        var valueCell = worksheet.Cell(row, 2);
        valueCell.Value = value;

        TrackWidth(columnWidths, 1, label.Length);
        TrackWidth(columnWidths, 2, value.Length);
    }

    private static int WriteSection(
        IXLWorksheet worksheet,
        int startRow,
        ReportTableSection section,
        TimeZoneInfo timeZone,
        Dictionary<int, int> columnWidths)
    {
        var row = startRow;
        if (!string.IsNullOrWhiteSpace(section.Title))
        {
            var titleCell = worksheet.Cell(row, 1);
            titleCell.Value = section.Title;
            titleCell.Style.Font.Bold = true;
            titleCell.Style.Font.FontSize = 11;
            TrackWidth(columnWidths, 1, section.Title.Length);
            row++;
        }

        for (var col = 0; col < section.Columns.Count; col++)
        {
            var colNumber = col + 1;
            var header = section.Columns[col].Header;
            var cell = worksheet.Cell(row, colNumber);
            cell.Value = header;
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E2E8F0");
            cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            TrackWidth(columnWidths, colNumber, header.Length);
        }
        row++;

        foreach (var dataRow in section.Rows)
        {
            for (var col = 0; col < section.Columns.Count; col++)
            {
                var colNumber = col + 1;
                var value = col < dataRow.Count ? dataRow[col] : null;
                var cell = worksheet.Cell(row, colNumber);
                var displayedLength = SetTypedCellValue(cell, section.Columns[col].Type, value, timeZone);
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                TrackWidth(columnWidths, colNumber, displayedLength);
            }
            row++;
        }

        if (!string.IsNullOrWhiteSpace(section.FooterNote))
        {
            var noteCell = worksheet.Cell(row, 1);
            noteCell.Value = section.FooterNote;
            noteCell.Style.Font.Italic = true;
            row++;
        }

        return row;
    }

    private static int SetTypedCellValue(
        IXLCell cell,
        ReportColumnType columnType,
        object? value,
        TimeZoneInfo timeZone)
    {
        if (value is null)
        {
            cell.Value = Blank.Value;
            return 0;
        }

        switch (columnType)
        {
            case ReportColumnType.Integer:
                var intNumber = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Value = intNumber;
                cell.Style.NumberFormat.Format = "#,##0";
                return intNumber.ToString("#,##0", CultureInfo.InvariantCulture).Length;

            case ReportColumnType.Decimal:
                var decimalNumber = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Value = decimalNumber;
                cell.Style.NumberFormat.Format = "#,##0.###";
                return decimalNumber.ToString("#,##0.###", CultureInfo.InvariantCulture).Length;

            case ReportColumnType.Money:
                var money = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Value = money;
                cell.Style.NumberFormat.Format = "#,##0 \"₫\"";
                return money.ToString("#,##0", CultureInfo.InvariantCulture).Length + 2;

            case ReportColumnType.Percent:
                var percent = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                cell.Value = percent;
                // Dùng "%" thoát nháy để giữ nguyên giá trị số phần trăm đã nhân 100 (ví dụ 25.0 -> 25.0%).
                cell.Style.NumberFormat.Format = "0.0\"%\"";
                return percent.ToString("0.0", CultureInfo.InvariantCulture).Length + 1;

            case ReportColumnType.Date:
                var dateOnly = value switch
                {
                    DateOnly d => d,
                    DateTime dt => DateOnly.FromDateTime(dt),
                    _ => DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
                };
                cell.Value = dateOnly.ToDateTime(TimeOnly.MinValue);
                cell.Style.NumberFormat.Format = "dd/MM/yyyy";
                return 10;

            case ReportColumnType.DateTime:
                var utcTime = value switch
                {
                    DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => DateTime.SpecifyKind(DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture), DateTimeKind.Utc),
                };
                cell.Value = TimeZoneInfo.ConvertTimeFromUtc(utcTime, timeZone);
                cell.Style.NumberFormat.Format = "dd/MM/yyyy HH:mm";
                return 16;

            default:
                var text = value.ToString() ?? string.Empty;
                cell.Value = text;
                return text.Length;
        }
    }

    private static void TrackWidth(Dictionary<int, int> columnWidths, int colIndex, int length)
    {
        if (!columnWidths.TryGetValue(colIndex, out var current) || length > current)
        {
            columnWidths[colIndex] = length;
        }
    }

    private static string SanitizeSheetName(string name)
    {
        var cleaned = string.Concat(name.Where(c => !_invalidSheetChars.Contains(c))).Trim();
        if (cleaned.Length == 0)
        {
            return "Báo cáo";
        }

        return cleaned.Length > 31 ? cleaned[..31] : cleaned;
    }
}
