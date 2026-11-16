using HouseholdFarm.Api.Domain.Entities;

namespace HouseholdFarm.Api.Features.Sales;

/// <summary>Giao dịch bán kèm doanh thu và đã thu tính trong cùng truy vấn SQL.</summary>
/// <remarks>Lớp có thuộc tính <c>init</c> (không phải record vị trí) để EF dịch tiếp được các bước lọc, sắp xếp phía sau.</remarks>
internal sealed class SaleAmountRow
{
    public required Sale Sale { get; init; }

    public decimal Revenue { get; init; }

    public decimal PaidAmount { get; init; }
}

internal static class SaleAmounts
{
    /// <summary>
    /// Doanh thu = số lượng × đơn giá, làm tròn 2 chữ số; đã thu = tổng các lần thanh toán chưa Void.
    /// Công nợ và trạng thái thanh toán tính tiếp từ hai giá trị này.
    /// </summary>
    public static IQueryable<SaleAmountRow> Of(IQueryable<Sale> sales) => sales.Select(s => new SaleAmountRow
    {
        Sale = s,
        Revenue = Math.Round(s.Quantity * s.UnitPrice, 2),
        PaidAmount = s.Payments.Where(p => p.VoidedAt == null).Sum(p => p.Amount),
    });
}
