namespace HouseholdFarm.Api.Features.Sales;

// Lớp tạm để module khác biên dịch và đăng ký DI được trước; logic thật làm ở FR-SAL-03.
public class SaleHarvestLinkService : ISaleHarvestLinkService
{
    public Task<IReadOnlyList<Guid>> RemoveByHarvestRecordAsync(Guid harvestRecordId, CancellationToken ct) =>
        throw new NotImplementedException();
}
