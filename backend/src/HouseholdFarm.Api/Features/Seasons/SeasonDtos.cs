using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Seasons;

/// <summary>
/// Thông tin trạng thái và ngày của mùa vụ do <see cref="ISeasonStateGuard"/> trả về.
/// Bên gọi (như Sales, Supplies) có thể sử dụng <c>StartDate</c> để kiểm tra ngày giao dịch không trước ngày bắt đầu mùa vụ (BR-SAL-01).
/// </summary>
/// <param name="Id">Id mùa vụ.</param>
/// <param name="Status">Trạng thái hiện tại của mùa vụ.</param>
/// <param name="StartDate">Ngày bắt đầu mùa vụ.</param>
/// <param name="ExpectedHarvestDate">Ngày dự kiến thu hoạch.</param>
/// <param name="PlotId">Thửa đất của mùa vụ.</param>
/// <param name="CropId">Cây trồng của mùa vụ.</param>
public record SeasonStateResponse(
    Guid Id,
    SeasonStatus Status,
    DateOnly StartDate,
    DateOnly ExpectedHarvestDate,
    Guid PlotId,
    Guid CropId);

