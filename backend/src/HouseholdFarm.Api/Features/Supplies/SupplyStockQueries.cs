using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>
/// Hàm tính tồn dùng chung của module: mọi nơi cần tồn (danh sách, chi tiết, kiểm tra tồn, <see cref="IStockService"/>)
/// đều đi qua đây. BR-SUP-01: tồn là tổng <c>quantity</c> có dấu của các giao dịch chưa Void, không lưu thành cột.
/// </summary>
public static class SupplyStockQueries
{
    /// <summary>Kèm tồn của từng vật tư trong cùng một câu lệnh, để danh sách không truy vấn trong vòng lặp.</summary>
    public static IQueryable<SupplyWithStock> WithStock(
        this IQueryable<Supply> supplies,
        IQueryable<StockTransaction> transactions) =>
        supplies.Select(s => new SupplyWithStock
        {
            Supply = s,
            StockQuantity = transactions
                .Where(t => t.SupplyId == s.Id && t.VoidedAt == null)
                .Sum(t => (decimal?)t.Quantity) ?? 0m,

            // BR-CAT-05: giao dịch đã Void vẫn tính là đã có giao dịch.
            HasStockTransactions = transactions.Any(t => t.SupplyId == s.Id),
        });

    /// <summary>Tồn của một vật tư. Thao tác thay đổi tồn gọi hàm này sau khi đã khóa dòng <c>supplies</c> (BR-GEN-12).</summary>
    public static async Task<decimal> StockQuantityOfAsync(
        this IQueryable<StockTransaction> transactions,
        Guid supplyId,
        CancellationToken ct) =>
        await transactions
            .Where(t => t.SupplyId == supplyId && t.VoidedAt == null)
            .SumAsync(t => (decimal?)t.Quantity, ct) ?? 0m;
}
