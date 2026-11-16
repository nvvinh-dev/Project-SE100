using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Reports.Finance;

public class SeasonFinanceService(AppDbContext db, ICurrentUser currentUser) : ISeasonFinanceService
{
    public async Task<IReadOnlyList<SeasonFinanceResponse>> GetAsync(
        IReadOnlyCollection<Guid> seasonIds,
        CancellationToken ct)
    {
        // BR-RPT-04: kiểm tra quyền trước cả khi danh sách rỗng, để bên gọi thiếu quyền luôn nhận FORBIDDEN.
        currentUser.EnsureCanManageFinance();

        if (seasonIds.Count == 0)
        {
            return [];
        }

        var ids = seasonIds.Distinct().ToList();

        // Global Query Filter chỉ trả mùa vụ của hộ hiện tại; mùa vụ của hộ khác coi như không tồn tại (BR-GEN-02).
        var statuses = await db.Seasons
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .Select(s => new { s.Id, s.Status })
            .ToDictionaryAsync(s => s.Id, s => s.Status, ct);

        if (statuses.Count != ids.Count)
        {
            throw AppException.NotFound("mùa vụ");
        }

        // BR-RPT-01: mọi tổng loại bản ghi đã Void. Mỗi số liệu một truy vấn gộp theo mùa vụ, không truy vấn trong vòng lặp.
        var supplyCosts = await db.StockTransactions
            .AsNoTracking()
            .Where(t => t.Type == StockTransactionType.Out
                && t.VoidedAt == null
                && t.SeasonId != null
                && ids.Contains(t.SeasonId.Value))
            .GroupBy(t => t.SeasonId!.Value)
            .Select(g => new { SeasonId = g.Key, Total = g.Sum(t => t.TotalCost) })
            .ToDictionaryAsync(x => x.SeasonId, x => x.Total, ct);

        var expenseAmounts = await db.Expenses
            .AsNoTracking()
            .Where(e => e.VoidedAt == null && e.SeasonId != null && ids.Contains(e.SeasonId.Value))
            .GroupBy(e => e.SeasonId!.Value)
            .Select(g => new { SeasonId = g.Key, Total = g.Sum(e => e.Amount) })
            .ToDictionaryAsync(x => x.SeasonId, x => x.Total, ct);

        // Doanh thu mỗi giao dịch bán làm tròn 2 chữ số rồi mới cộng (data model mục 6).
        var revenues = await db.Sales
            .AsNoTracking()
            .Where(s => s.VoidedAt == null && ids.Contains(s.SeasonId))
            .GroupBy(s => s.SeasonId)
            .Select(g => new { SeasonId = g.Key, Total = g.Sum(s => Math.Round(s.Quantity * s.UnitPrice, 2)) })
            .ToDictionaryAsync(x => x.SeasonId, x => x.Total, ct);

        var paidAmounts = await db.Payments
            .AsNoTracking()
            .Where(p => p.VoidedAt == null && p.Sale.VoidedAt == null && ids.Contains(p.Sale.SeasonId))
            .GroupBy(p => p.Sale.SeasonId)
            .Select(g => new { SeasonId = g.Key, Total = g.Sum(p => p.Amount) })
            .ToDictionaryAsync(x => x.SeasonId, x => x.Total, ct);

        return ids
            .Select(id =>
            {
                var supplyCost = supplyCosts.GetValueOrDefault(id);
                var expenseAmount = expenseAmounts.GetValueOrDefault(id);
                var revenue = revenues.GetValueOrDefault(id);
                var paidAmount = paidAmounts.GetValueOrDefault(id);

                return new SeasonFinanceResponse(
                    id,
                    statuses[id],
                    supplyCost,
                    expenseAmount,
                    revenue,
                    paidAmount,
                    revenue - paidAmount,
                    revenue - supplyCost - expenseAmount);
            })
            .ToList();
    }
}
