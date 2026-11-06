namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>Công thức giá vốn dùng chung cho mọi loại giao dịch kho, để không có hai nơi tính khác nhau.</summary>
public static class StockCosting
{
    private const int AverageUnitCostDecimals = 4;
    private const int TotalCostDecimals = 2;

    /// <summary>
    /// BR-SUP-04: giá bình quân sau khi nhận <paramref name="receivedQuantity"/> vào kho với <paramref name="unitCost"/>,
    /// dùng khi nhập kho và khi hủy giao dịch làm giảm tồn. Tồn không dương thì giá cũ không còn ý nghĩa, lấy luôn đơn giá mới.
    /// </summary>
    /// <param name="currentStock">Tồn ngay trước khi nhận, tính sau khi đã khóa dòng <c>supplies</c>.</param>
    public static decimal AverageAfterReceipt(
        decimal currentStock,
        decimal currentAverage,
        decimal receivedQuantity,
        decimal unitCost)
    {
        var average = currentStock > 0
            ? ((currentStock * currentAverage) + (receivedQuantity * unitCost)) / (currentStock + receivedQuantity)
            : unitCost;
        return decimal.Round(average, AverageUnitCostDecimals, MidpointRounding.AwayFromZero);
    }

    /// <summary>Giá trị giao dịch: trị tuyệt đối của số lượng × đơn giá, luôn không âm, lưu cố định lúc tạo.</summary>
    /// <param name="quantity">Số lượng có dấu như trong database.</param>
    public static decimal TotalCost(decimal quantity, decimal unitCost) =>
        decimal.Round(Math.Abs(quantity) * unitCost, TotalCostDecimals, MidpointRounding.AwayFromZero);
}
