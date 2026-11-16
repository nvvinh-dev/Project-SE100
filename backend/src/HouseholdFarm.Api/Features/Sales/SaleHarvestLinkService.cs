using HouseholdFarm.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Sales;

public sealed class SaleHarvestLinkService(AppDbContext db) : ISaleHarvestLinkService
{
    public async Task<IReadOnlyList<Guid>> RemoveByHarvestRecordAsync(Guid harvestRecordId, CancellationToken ct)
    {
        // Bên gọi (sửa, hủy phiếu thu hoạch) đã mở giao dịch thì dùng chung, để gỡ liên kết và sửa phiếu cùng thành công hoặc cùng hủy.
        var ownTransaction = db.Database.CurrentTransaction is null
            ? await db.Database.BeginTransactionAsync(ct)
            : null;

        try
        {
            // BR-HAR-03, BR-HAR-04: xóa thật dòng liên kết (bảng nối được phép xóa thật khi gỡ liên kết).
            var links = await db.SaleHarvestLinks.Where(l => l.HarvestRecordId == harvestRecordId).ToListAsync(ct);
            db.SaleHarvestLinks.RemoveRange(links);
            await db.SaveChangesAsync(ct);

            if (ownTransaction is not null)
            {
                await ownTransaction.CommitAsync(ct);
            }

            return links.Select(l => l.SaleId).Distinct().ToList();
        }
        finally
        {
            if (ownTransaction is not null)
            {
                await ownTransaction.DisposeAsync();
            }
        }
    }
}
