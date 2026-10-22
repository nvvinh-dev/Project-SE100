namespace HouseholdFarm.Api.Features.Supplies;

// Lớp tạm để module khác biên dịch và đăng ký DI được trước; logic thật làm ở FR-SUP-10.
public class StockService : IStockService
{
    public Task<IReadOnlyList<SupplyStockResponse>> GetStockAsync(
        IReadOnlyCollection<Guid> supplyIds,
        CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<StockTransactionResponse> StockOutForFarmTaskAsync(
        Guid farmTaskId,
        Guid supplyId,
        decimal quantity,
        DateOnly? performedDate,
        CancellationToken ct) =>
        throw new NotImplementedException();

    public Task VoidFarmTaskStockOutAsync(Guid stockTransactionId, string reason, CancellationToken ct) =>
        throw new NotImplementedException();
}
