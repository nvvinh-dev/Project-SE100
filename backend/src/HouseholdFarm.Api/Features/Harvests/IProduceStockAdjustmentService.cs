using HouseholdFarm.Api.Common.Responses;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>Nghiệp vụ điều chỉnh giảm và hủy điều chỉnh tồn nông sản cho Chủ hộ (FR-HAR-07).</summary>
public interface IProduceStockAdjustmentService
{
    Task<PagedResult<ProduceStockAdjustmentResponse>> GetListAsync(
        ProduceStockAdjustmentQuery query,
        CancellationToken ct);

    Task<ProduceStockAdjustmentResponse> CreateAsync(
        CreateProduceStockAdjustmentRequest request,
        CancellationToken ct);

    Task<ProduceStockAdjustmentResponse> VoidAsync(
        Guid id,
        VoidProduceStockAdjustmentRequest request,
        CancellationToken ct);
}
