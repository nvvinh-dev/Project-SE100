using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace HouseholdFarm.Api.Features.Reports.Common;

/// <summary>
/// Tạo tệp PDF khổ A4 từ cùng <see cref="ReportExportDocument"/> của bộ xuất Excel (FR-RPT-05).
/// </summary>
public sealed class PdfReportWriter
{
    private static readonly CultureInfo _vi = CultureInfo.GetCultureInfo("vi-VN");

    static PdfReportWriter()
    {
        // Khai báo giấy phép Community cả ở đây để các test gọi trực tiếp không qua Program.cs vẫn chạy được.
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Write(ReportExportDocument document, DateTime exportedAtUtc, TimeZoneInfo timeZone)
    {
        var exportedLocal = TimeZoneInfo.ConvertTimeFromUtc(
            DateTime.SpecifyKind(exportedAtUtc, DateTimeKind.Utc),
            timeZone);
        var useLandscape = document.Sections.Any(s => s.Columns.Count > 6);
        var pageSize = useLandscape ? PageSizes.A4.Landscape() : PageSizes.A4;

        var pdf = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(pageSize);
                page.Margin(24);
                page.PageColor(Colors.White);
                // Lato đi kèm sẵn trong QuestPDF 2026.9.1, không phụ thuộc font hệ thống khi chạy trên Linux/CI hay Windows.
                page.DefaultTextStyle(style => style
                    .FontFamily("Lato")
                    .FontSize(9)
                    .FontColor(Colors.Grey.Darken4));

                page.Header().Column(col =>
                {
                    col.Item().Text(document.Title).FontSize(15).Bold().FontColor(Colors.Green.Darken3);
                    col.Item().PaddingTop(2).Text($"Ngày xuất: {exportedLocal.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)}")
                        .FontSize(8.5f)
                        .FontColor(Colors.Grey.Darken2);
                    col.Item().PaddingTop(6).LineHorizontal(1).LineColor(Colors.Grey.Lighten2);
                });

                page.Content().PaddingVertical(10).Column(col =>
                {
                    col.Spacing(10);

                    if (document.FiltersSummary.Count > 0)
                    {
                        col.Item().Element(c => ComposeSummaryBlock(c, "Bộ lọc áp dụng", document.FiltersSummary));
                    }

                    if (document.OverviewItems.Count > 0)
                    {
                        col.Item().Element(c => ComposeSummaryBlock(c, "Tổng quan", document.OverviewItems));
                    }

                    foreach (var section in document.Sections)
                    {
                        col.Item().Element(c => ComposeTableSection(c, section, timeZone));
                    }
                });

                page.Footer().Row(row =>
                {
                    row.RelativeItem().Text("Hệ thống quản lý nông nghiệp hộ gia đình")
                        .FontSize(8)
                        .FontColor(Colors.Grey.Darken1);
                    row.AutoItem().Text(text =>
                    {
                        text.Span("Trang ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.Span(" / ").FontSize(8).FontColor(Colors.Grey.Darken1);
                        text.TotalPages().FontSize(8).FontColor(Colors.Grey.Darken1);
                    });
                });
            });
        });

        return pdf.GeneratePdf();
    }

    private static void ComposeSummaryBlock(IContainer container, string heading, IReadOnlyList<ReportSummaryItem> items)
    {
        container
            .Border(1)
            .BorderColor(Colors.Grey.Lighten2)
            .Background(Colors.Grey.Lighten5)
            .Padding(8)
            .Column(col =>
            {
                col.Spacing(4);
                col.Item().Text(heading).Bold().FontSize(10);
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.ConstantColumn(150);
                        columns.RelativeColumn();
                    });

                    foreach (var item in items)
                    {
                        table.Cell().PaddingVertical(1).Text(item.Label).FontColor(Colors.Grey.Darken2);
                        table.Cell().PaddingVertical(1).Text(item.Value).SemiBold();
                    }
                });
            });
    }

    private static void ComposeTableSection(IContainer container, ReportTableSection section, TimeZoneInfo timeZone)
    {
        container.Column(col =>
        {
            col.Spacing(4);

            if (!string.IsNullOrWhiteSpace(section.Title))
            {
                col.Item().Text(section.Title).Bold().FontSize(11);
            }

            if (section.Columns.Count > 0)
            {
                col.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        foreach (var column in section.Columns)
                        {
                            var weight = column.Type == ReportColumnType.Text ? 1.5f : 1f;
                            columns.RelativeColumn(weight);
                        }
                    });

                    table.Header(header =>
                    {
                        foreach (var column in section.Columns)
                        {
                            var cell = header.Cell()
                                .Background(Colors.Grey.Lighten3)
                                .Border(0.5f)
                                .BorderColor(Colors.Grey.Lighten1)
                                .PaddingHorizontal(5)
                                .PaddingVertical(4);

                            var text = IsNumericColumn(column.Type)
                                ? cell.AlignRight().Text(column.Header)
                                : cell.Text(column.Header);
                            text.Bold().FontSize(8.5f);
                        }
                    });

                    if (section.Rows.Count == 0)
                    {
                        table.Cell()
                            .ColumnSpan((uint)section.Columns.Count)
                            .Border(0.5f)
                            .BorderColor(Colors.Grey.Lighten2)
                            .Padding(6)
                            .Text("Không có dữ liệu.")
                            .Italic()
                            .FontColor(Colors.Grey.Darken1);
                    }
                    else
                    {
                        foreach (var dataRow in section.Rows)
                        {
                            for (var index = 0; index < section.Columns.Count; index++)
                            {
                                var column = section.Columns[index];
                                var raw = index < dataRow.Count ? dataRow[index] : null;
                                var formatted = FormatCellValue(column.Type, raw, timeZone);

                                var cell = table.Cell()
                                    .Border(0.5f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .PaddingHorizontal(5)
                                    .PaddingVertical(3.5f);

                                var text = IsNumericColumn(column.Type)
                                    ? cell.AlignRight().Text(formatted)
                                    : cell.Text(formatted);
                                text.FontSize(8.5f);
                            }
                        }
                    }
                });
            }

            if (!string.IsNullOrWhiteSpace(section.FooterNote))
            {
                col.Item().Text(section.FooterNote).Italic().FontSize(8).FontColor(Colors.Grey.Darken2);
            }
        });
    }

    private static bool IsNumericColumn(ReportColumnType columnType) =>
        columnType is ReportColumnType.Integer or ReportColumnType.Decimal or ReportColumnType.Money or ReportColumnType.Percent;

    private static string FormatCellValue(ReportColumnType columnType, object? value, TimeZoneInfo timeZone)
    {
        if (value is null)
        {
            return "—";
        }

        return columnType switch
        {
            ReportColumnType.Integer => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("#,##0", _vi),
            ReportColumnType.Decimal => Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("#,##0.###", _vi),
            ReportColumnType.Money => $"{Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("#,##0", _vi)} ₫",
            ReportColumnType.Percent => $"{Convert.ToDecimal(value, CultureInfo.InvariantCulture).ToString("0.0", _vi)}%",
            ReportColumnType.Date => (value switch
            {
                DateOnly d => d,
                DateTime dt => DateOnly.FromDateTime(dt),
                _ => DateOnly.Parse(value.ToString()!, CultureInfo.InvariantCulture),
            }).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            ReportColumnType.DateTime => TimeZoneInfo.ConvertTimeFromUtc(
                value switch
                {
                    DateTime dt => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
                    DateTimeOffset dto => dto.UtcDateTime,
                    _ => DateTime.SpecifyKind(DateTime.Parse(value.ToString()!, CultureInfo.InvariantCulture), DateTimeKind.Utc),
                },
                timeZone).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }
}
