using HouseholdFarm.Api.Common.Auditing;
using HouseholdFarm.Api.Common.Auth;
using HouseholdFarm.Api.Common.Errors;
using HouseholdFarm.Api.Common.Persistence;
using HouseholdFarm.Api.Data;
using HouseholdFarm.Api.Domain.Entities;
using HouseholdFarm.Api.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace HouseholdFarm.Api.Features.Supplies;

public sealed class StockTransactionService(
    AppDbContext db,
    ICurrentUser currentUser,
    IAuditLogger audit,
    StockLedger ledger) : IStockTransactionService
{
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
            _ => throw new ArgumentOutOfRangeException(nameof(request), request.Type, null),
        };
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

        audit.Log(AuditActions.StockTransactionStockIn, transaction.Id, oldValues: null, AuditValues(transaction), reason: null);

        if (await db.SaveChangesOrFindDuplicateAsync(transaction, ct) is { } duplicate)
        {
            return ToResponse(duplicate);
        }

        await tx.CommitAsync(ct);
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

    private static object AuditValues(StockTransaction t) => new
    {
        t.SupplyId,
        t.Type,
        t.SeasonId,
        t.TransactionDate,
        t.Quantity,
        t.UnitCost,
        t.AverageUnitCostBefore,
        t.TotalCost,
    };

    private static StockTransactionResponse ToResponse(StockTransaction t) => new(
        t.Id,
        t.SupplyId,
        t.Type,
        t.SeasonId,
        t.TransactionDate,
        t.Quantity,
        t.UnitCost,
        t.TotalCost,
        t.Reason,
        t.Note,
        t.VoidedAt,
        t.Version);

    private static string? TrimToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
