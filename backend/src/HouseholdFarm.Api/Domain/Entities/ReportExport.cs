using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

// Chỉ ghi thêm. Không lưu file báo cáo: file được tạo và trả thẳng cho người dùng tải.
public class ReportExport : IHouseholdOwned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public ReportType ReportType { get; set; }

    public ExportFormat Format { get; set; }

    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    // JSON chứa toàn bộ bộ lọc đã dùng.
    public string Filters { get; set; } = string.Empty;

    public Guid ExportedById { get; set; }

    public DateTime ExportedAt { get; set; }
}
