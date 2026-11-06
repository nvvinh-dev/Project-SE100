using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Common.Responses;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using HouseholdFarm.Api.Features.Seasons;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Supplies;

public sealed class StockTransactionService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    ISeasonStateGuard seasonStateGuard,
    StockLedger ledger,
    StockOperations operations) : IStockTransactionService
{
    public async Task<PagedResult<StockTransactionListItem>> GetListAsync(StockTransactionQuery query, CancellationToken ct)
    {
        currentUser.EnsureCanManageStock();

        var transactions = db.StockTransactions.AsNoTracking();

        if (query.IncludeVoided != true)
        {
            transactions = transactions.Where(t => t.VoidedAt == null);
        }

        if (query.SupplyId is { } supplyId)
        {
            transactions = transactions.Where(t => t.SupplyId == supplyId);
        }

        if (query.Type is { } type)
        {
            transactions = transactions.Where(t => t.Type == type);
        }

        if (query.SeasonId is { } seasonId)
        {
            transactions = transactions.Where(t => t.SeasonId == seasonId);
        }

        if (query.From is { } from)
        {
            transactions = transactions.Where(t => t.TransactionDate >= from);
        }

        if (query.To is { } to)
        {
            transactions = transactions.Where(t => t.TransactionDate <= to);
        }

        return await transactions
            .OrderByDescending(t => t.TransactionDate)
            .ThenByDescending(t => t.CreatedAt)
            .ThenBy(t => t.Id)
            .Select(t => new StockTransactionListItem(
                t.Id,
                t.TransactionDate,
                t.SupplyId,
                t.Supply.Code,
                t.Supply.Name,
                t.Supply.Unit,
                t.Type,
                t.Quantity,
                t.UnitCost,
                t.TotalCost,
                t.SeasonId,
                t.Season == null ? null : t.Season.Crop.Name,
                t.Season == null ? null : t.Season.Plot.Code,
                t.Season == null ? null : t.Season.StartDate,
                t.Reason,
                t.Note,
                t.VoidedAt,
                t.VoidReason,
                db.FarmTaskSupplyUsages.Any(u => u.StockTransactionId == t.Id)))
            .ToPagedResultAsync(query, ct);
    }

    public async Task<StockTransactionResponse> GetAsync(Guid id, CancellationToken ct)
    {
        currentUser.EnsureCanManageStock();

        var transaction = await db.StockTransactions.AsNoTracking().SingleOrDefaultAsync(t => t.Id == id, ct)
            ?? throw AppException.NotFound("giao dịch kho");
        return ToResponse(transaction);
    }

    public Task<StockTransactionResponse> CreateAsync(CreateStockTransactionRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageStock();

        return request.Type switch
        {
            StockTransactionType.In => StockInAsync(request, ct),
            StockTransactionType.Out => StockOutAsync(request, ct),
            StockTransactionType.Adjustment => AdjustAsync(request, ct),
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Type, null),
        };
    }

    public async Task<StockTransactionResponse> VoidAsync(Guid id, VoidStockTransactionRequest request, CancellationToken ct)
    {
        currentUser.EnsureCanManageStock();

        var target = await db.StockTransactions
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(t => new
            {
                t.SupplyId,
                t.SeasonId,
                t.VoidedAt,
                IsFromFarmTask = db.FarmTaskSupplyUsages.Any(u => u.StockTransactionId == t.Id),
            })
            .SingleOrDefaultAsync(ct)
            ?? throw AppException.NotFound("giao dịch kho");

        // Báo trước khi kiểm tra mùa vụ: hủy lần hai, hoặc hủy giao dịch của công việc thì lý do thật là ở đây,
        // không phải vì mùa vụ đã chốt.
        if (target.VoidedAt is not null)
        {
            throw StockOperations.AlreadyVoided();
        }

        if (target.IsFromFarmTask)
        {
            // BR-TSK-11: dòng vật tư của công việc đang trỏ tới giao dịch này, hủy ở đây sẽ làm công việc sai với kho.
            throw new AppException(
                ErrorCodes.StockTxManagedByTask,
                "Giao dịch xuất này sinh từ công việc. Hãy sửa lượng vật tư đã dùng trong công việc đó.");
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        if (target.SeasonId is { } seasonId)
        {
            // BR-GEN-12: khóa seasons trước supplies, để mùa vụ không bị chốt, hủy giữa lúc kiểm tra và lúc ghi.
            await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.StockOut, forUpdate: true, ct);
        }

        var supply = await ledger.LockSupplyAsync(target.SupplyId, ct);
        var transaction = await operations.ReadForVoidAsync(id, ct);

        await operations.VoidAsync(transaction, supply, request.Reason, ct);

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return ToResponse(transaction);
    }

    private async Task<StockTransactionResponse> StockInAsync(CreateStockTransactionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-GEN-12: khóa trước khi tính tồn, để hai lần nhập cùng lúc không tính giá bình quân trên cùng một mức tồn.
        var supply = await ledger.LockSupplyAsync(request.SupplyId, ct);

        // Tìm sau khi khóa: yêu cầu gửi trùng đang chờ khóa phải thấy giao dịch lần trước vừa lưu (BR-GEN-10).
        if (await db.StockTransactions.FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return ToResponse(existing);
        }

        EnsureActive(supply);

        var stock = await db.StockTransactions.StockQuantityOfAsync(supply.Id, ct);
        var unitCost = request.UnitCost!.Value;

        // BR-SUP-05: không gắn mùa vụ, không tạo chi phí mùa vụ.
        var transaction = new StockTransaction
        {
            SupplyId = supply.Id,
            Type = StockTransactionType.In,
            TransactionDate = request.TransactionDate!.Value,
            Quantity = request.Quantity,
            UnitCost = unitCost,
            AverageUnitCostBefore = supply.AverageUnitCost,
            TotalCost = StockCosting.TotalCost(request.Quantity, unitCost),
            Note = TrimToNull(request.Note),
            RequestId = request.RequestId,
        };
        supply.AverageUnitCost = StockCosting.AverageAfterReceipt(stock, supply.AverageUnitCost, request.Quantity, unitCost);
        db.StockTransactions.Add(transaction);

        audit.Log(
            AuditActions.StockTransactionStockIn,
            transaction.Id,
            oldValues: null,
            StockOperations.AuditValues(transaction),
            reason: null);

        return await SaveAsync(transaction, tx.CommitAsync, ct);
    }

    private async Task<StockTransactionResponse> StockOutAsync(CreateStockTransactionRequest request, CancellationToken ct)
    {
        // Yêu cầu gửi lại khi mùa vụ đã chốt vẫn phải nhận kết quả lần trước, nên tìm trước cả bước kiểm tra mùa vụ.
        if (await db.StockTransactions.AsNoTracking().FindByRequestIdAsync(request.RequestId, ct) is { } earlier)
        {
            return ToResponse(earlier);
        }

        await using var tx = await db.Database.BeginTransactionAsync(ct);

        // BR-GEN-12: khóa seasons rồi supplies. BR-USR-13: quyền Kho được chọn mùa vụ ngoài phân công.
        var seasonId = request.SeasonId!.Value;
        await seasonStateGuard.EnsureOperationAllowedAsync(seasonId, SeasonOperation.StockOut, forUpdate: true, ct);
        var supply = await ledger.LockSupplyAsync(request.SupplyId, ct);

        // Tìm lại sau khi khóa: yêu cầu trùng chạy song song đang chờ khóa phải thấy giao dịch vừa lưu (BR-GEN-10).
        if (await db.StockTransactions.FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return ToResponse(existing);
        }

        EnsureActive(supply);

        var transaction = await operations.AddStockOutAsync(
            supply,
            seasonId,
            request.Quantity,
            request.TransactionDate!.Value,
            TrimToNull(request.Note),
            request.RequestId,
            ct);

        return await SaveAsync(transaction, tx.CommitAsync, ct);
    }

    private async Task<StockTransactionResponse> AdjustAsync(CreateStockTransactionRequest request, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);

        var supply = await ledger.LockSupplyAsync(request.SupplyId, ct);

        if (await db.StockTransactions.FindByRequestIdAsync(request.RequestId, ct) is { } existing)
        {
            return ToResponse(existing);
        }

        // BR-GEN-13: vật tư Inactive vẫn điều chỉnh được, để dọn tồn còn lại, nên không kiểm tra trạng thái.
        if (request.Quantity < 0)
        {
            var stock = await db.StockTransactions.StockQuantityOfAsync(supply.Id, ct);
            await ledger.CheckDecreaseAsync(supply, stock, -request.Quantity, "giảm", ct);
        }

        // BR-SUP-09: đơn giá là giá bình quân hiện tại, giá bình quân không đổi; không gắn mùa vụ.
        var reason = request.Reason!.Trim();
        var transaction = new StockTransaction
        {
            SupplyId = supply.Id,
            Type = StockTransactionType.Adjustment,
            TransactionDate = request.TransactionDate!.Value,
            Quantity = request.Quantity,
            UnitCost = supply.AverageUnitCost,
            AverageUnitCostBefore = supply.AverageUnitCost,
            TotalCost = StockCosting.TotalCost(request.Quantity, supply.AverageUnitCost),
            Reason = reason,
            Note = TrimToNull(request.Note),
            RequestId = request.RequestId,
        };
        db.StockTransactions.Add(transaction);

        audit.Log(
            AuditActions.StockTransactionAdjust,
            transaction.Id,
            oldValues: null,
            StockOperations.AuditValues(transaction),
            reason);

        return await SaveAsync(transaction, tx.CommitAsync, ct);
    }

    // Gặp yêu cầu trùng chạy song song thì hàm lưu đã hủy giao dịch database, trả bản ghi đã có và không commit (BR-GEN-10).
    private async Task<StockTransactionResponse> SaveAsync(
        StockTransaction transaction,
        Func<CancellationToken, Task> commitAsync,
        CancellationToken ct)
    {
        if (await db.SaveChangesOrFindDuplicateAsync(transaction, ct) is { } duplicate)
        {
            return ToResponse(duplicate);
        }

        await commitAsync(ct);
        return ToResponse(transaction);
    }

    // BR-GEN-13: vật tư ngừng sử dụng không chọn được cho giao dịch mới.
    private static void EnsureActive(Supply supply)
    {
        if (supply.Status != RecordStatus.Active)
        {
            throw new AppException(ErrorCodes.CatalogInactive, $"Vật tư {supply.Name} đang ngừng sử dụng.");
        }
    }

    private static StockTransactionResponse ToResponse(StockTransaction t) => StockTransactionResponse.From(t);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
