using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Common;

// Nhãn tiếng Việt dùng chung khi dựng nội dung xuất Excel/PDF cho các báo cáo (FR-RPT-04, FR-RPT-05).
internal static class ReportLabels
{
    public static string Of(Unit unit) => unit switch
    {
        Unit.Kilogram => "kg",
        Unit.Gram => "g",
        Unit.Quintal => "tạ",
        Unit.Ton => "tấn",
        Unit.Liter => "lít",
        Unit.Milliliter => "ml",
        Unit.Bag => "bao",
        Unit.Bottle => "chai",
        Unit.Pack => "gói",
        Unit.Plant => "cây",
        Unit.Fruit => "quả",
        Unit.Bunch => "bó",
        _ => unit.ToString(),
    };

    public static string Of(SeasonStatus status) => status switch
    {
        SeasonStatus.Planned => "Lên kế hoạch",
        SeasonStatus.InProgress => "Đang canh tác",
        SeasonStatus.Closed => "Đã chốt",
        SeasonStatus.Cancelled => "Đã hủy",
        _ => status.ToString(),
    };
}
