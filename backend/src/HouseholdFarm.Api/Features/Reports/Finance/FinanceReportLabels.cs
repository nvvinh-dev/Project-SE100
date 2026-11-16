using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Reports.Finance;

// Nhãn tiếng Việt cho file xuất, đúng nhãn hiển thị trong bảng thuật ngữ; giao diện dùng bản của frontend.
internal static class FinanceReportLabels
{
    public static string Of(SupplyGroup group) => group switch
    {
        SupplyGroup.Seed => "Giống",
        SupplyGroup.Fertilizer => "Phân bón",
        SupplyGroup.Pesticide => "Thuốc bảo vệ thực vật",
        SupplyGroup.IrrigationMaterial => "Vật liệu tưới",
        SupplyGroup.Consumable => "Hàng tiêu hao khác",
        _ => group.ToString(),
    };

    public static string Of(ExpenseCategory category) => category switch
    {
        ExpenseCategory.Utilities => "Điện nước",
        ExpenseCategory.Labor => "Thuê nhân công",
        ExpenseCategory.Transport => "Vận chuyển",
        ExpenseCategory.Other => "Khác",
        _ => category.ToString(),
    };

    public static string Of(SalesChannel? channel) => channel switch
    {
        null => "Không ghi kênh",
        SalesChannel.Trader => "Thương lái",
        SalesChannel.WholesaleMarket => "Chợ đầu mối",
        SalesChannel.Retail => "Bán lẻ",
        SalesChannel.Cooperative => "Hợp tác xã",
        SalesChannel.Online => "Bán trực tuyến",
        SalesChannel.Other => "Khác",
        _ => channel.Value.ToString(),
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
