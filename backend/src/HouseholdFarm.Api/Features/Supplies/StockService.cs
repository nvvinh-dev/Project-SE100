using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Time;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace HouseholdFarm.Api.Features.Supplies;

public sealed class StockService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAppClock clock,
    ISeasonStateGuard seasonStateGuard,
    StockLedger ledger,
    StockOperations operations) : IStockService
{
    public async Task<IReadOnlyList<SupplyStockResponse>> GetStockAsync(
        IReadOnlyCollection<Guid> supplyIds,
        CancellationToken ct)
    {
        currentUser.EnsureNotWorker();

        var ids = supplyIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var rows = await db.Supplies
            .AsNoTracking()
            .Where(s => ids.Contains(s.Id))
            .WithStock(db.StockTransactions)
            .Select(r => new SupplyStockResponse(r.Supply.Id, r.StockQuantity, r.Supply.MinStockLevel))
            .ToListAsync(ct);

        return rows.Count == ids.Count ? rows : throw AppException.NotFound("vật tư");
    }

    public async Task<StockTransactionResponse> StockOutForFarmTaskAsync(
        Guid farmTaskId,
        Guid supplyId,
        decimal quantity,
        DateOnly? performedDate,
        CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(quantity);

        var seasonId = await db.FarmTasks
            .Where(t => t.Id == farmTaskId)
            .Select(t => (Guid?)t.SeasonId)
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("công việc");

        await using var ownTransaction = await BeginUnlessJoinedAsync(ct);

        await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.RecordFarmTaskResult, forUpdate: true, ct);
        var supply = await ledger.LockSupplyAsync(supplyId, ct);

        // BR-TSK-08: một lần ghi kết quả có thể tạo nhiều giao dịch nên backend tự sinh request_id;
        // chống ghi trùng dựa vào stock_transaction_id của dòng vật tư, do bên gọi giữ.
        var transaction = await operations.AddStockOutAsync(
            supply,
            seasonId,
            quantity,
            performedDate ?? clock.Today,
            note: null,
            Guid.CreateVersion7(),
            ct);

        await SaveAsync(ownTransaction, ct);
        return StockTransactionResponse.From(transaction);
    }

    public async Task VoidFarmTaskStockOutAsync(Guid stockTransactionId, string reason, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw AppException.Validation(new Dictionary<string, string[]>
            {
                ["reason"] = ["Vui lòng nhập lý do."],
            });
        }

        // Chỉ nhận giao dịch mà dòng vật tư của công việc đang trỏ tới: hàm không kiểm tra quyền Kho, nên không được thành
        // đường hủy mọi giao dịch kho.
        var target = await db.StockTransactions
            .AsNoTracking()
            .Where(t => t.Id == stockTransactionId
                && t.Type == StockTransactionType.Out
                && db.FarmTaskSupplyUsages.Any(u => u.StockTransactionId == t.Id))
            .Select(t => new { t.SupplyId, SeasonId = t.SeasonId!.Value, t.VoidedAt })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("giao dịch kho");

        if (target.VoidedAt is not null)
        {
            throw StockOperations.AlreadyVoided();
        }

        await using var ownTransaction = await BeginUnlessJoinedAsync(ct);

        await seasonStateGuard.EnsureOperationAllowedAsync(
            target.SeasonId,
            SeasonOperation.RecordFarmTaskResult,
            forUpdate: true,
            ct);
        var supply = await ledger.LockSupplyAsync(target.SupplyId, ct);
        var transaction = await operations.ReadForVoidAsync(stockTransactionId, ct);

        await operations.VoidAsync(transaction, supply, reason, ct);

        await SaveAsync(ownTransaction, ct);
    }

    // Đã có giao dịch của bên gọi thì tham gia, không mở mới: hoặc lưu hết, hoặc không lưu gì.
    private async Task<IDbContextTransaction?> BeginUnlessJoinedAsync(CancellationToken ct) =>
        db.Database.CurrentTransaction is null ? await db.Database.BeginTransactionAsync(ct) : null;

    // Luôn lưu để lần gọi sau trong cùng giao dịch tính đúng tồn; chỉ commit khi chính hàm này mở giao dịch.
    private async Task SaveAsync(IDbContextTransaction? ownTransaction, CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);
        if (ownTransaction is not null)
        {
            await ownTransaction.CommitAsync(ct);
        }
    }
}
