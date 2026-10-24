using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Harvests;

// Lớp tạm để module khác biên dịch và đăng ký DI được trước; logic thật làm ở [FR-HAR-08].
public class ProduceStockService : IProduceStockService
{
    public Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(Guid seasonId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task EnsureSufficientStockForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        decimal quantity,
        Guid? currentSaleId,
        CancellationToken ct) =>
        throw new NotImplementedException();

    public Task EnsureZeroStockAsync(Guid seasonId, CancellationToken ct) =>
        throw new NotImplementedException();

    public Task ValidateHarvestLinksForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        IReadOnlyCollection<Guid> harvestRecordIds,
        CancellationToken ct) =>
        throw new NotImplementedException();
}

