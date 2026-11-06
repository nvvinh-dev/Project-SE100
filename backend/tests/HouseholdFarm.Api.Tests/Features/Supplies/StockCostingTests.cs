using HouseholdFarm.Api.Features.Supplies;

namespace HouseholdFarm.Api.Tests.Features.Supplies;

/// <summary>Công thức giá vốn (FR-SUP-05).</summary>
public class StockCostingTests
{
    [Theory(DisplayName = "BR-SUP-04: giá bình quân sau khi nhận hàng")]
    [InlineData(10, 20000, 10, 24000, 22000)] // Bước 2 của ví dụ giá vốn.
    [InlineData(5, 22000, 5, 30000, 26000)] // Bước 4.
    [InlineData(10, 26000, 15, 22000, 23600)] // Bước 5: hủy giao dịch xuất coi như nhập lại.
    [InlineData(0, 0, 10, 20000, 20000)] // Tồn 0: lấy luôn đơn giá mới.
    [InlineData(-3, 15000, 5, 30000, 30000)] // Tồn âm: lấy luôn đơn giá mới.
    [InlineData(2, 10, 1, 11, 10.3333)] // Làm tròn 4 chữ số.
    public void AverageAfterReceipt_FollowsFormula(
        double stock,
        double average,
        double received,
        double unitCost,
        double expected)
    {
        var result = StockCosting.AverageAfterReceipt((decimal)stock, (decimal)average, (decimal)received, (decimal)unitCost);

        Assert.Equal((decimal)expected, result);
    }

    [Theory(DisplayName = "Giá trị giao dịch luôn không âm, làm tròn 2 chữ số")]
    [InlineData(15, 22000, 330000)]
    [InlineData(-15, 22000, 330000)]
    [InlineData(0.333, 1000.5, 333.17)]
    public void TotalCost_IsAbsoluteAndRounded(double quantity, double unitCost, double expected)
    {
        Assert.Equal((decimal)expected, StockCosting.TotalCost((decimal)quantity, (decimal)unitCost));
    }
}
