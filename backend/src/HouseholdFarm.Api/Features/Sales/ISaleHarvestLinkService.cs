namespace HouseholdFarm.Api.Features.Sales;

/// <summary>
/// Điểm nối bán hàng cho thu hoạch: gỡ liên kết giao dịch bán - phiếu thu hoạch khi phiếu không còn khớp
/// với giao dịch bán đã liên kết.
/// </summary>
public interface ISaleHarvestLinkService
{
    /// <summary>
    /// Xóa mọi dòng <c>sale_harvest_links</c> của một phiếu thu hoạch, dùng khi phiếu bị <c>Void</c> (BR-HAR-04)
    /// hoặc bị đổi <c>quality_grade</c> (BR-HAR-03).
    /// </summary>
    /// <param name="harvestRecordId">Phiếu thu hoạch cần gỡ liên kết; phiếu chưa có liên kết nào thì không làm gì.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Id các giao dịch bán vừa bị gỡ liên kết, để bên gọi đưa vào nhật ký của phiếu nếu cần.</returns>
    /// <remarks>
    /// Dùng giao dịch database của bên gọi nếu đã có, không mở giao dịch mới; chưa có thì tự mở và tự commit.
    /// Tự gọi <c>SaveChangesAsync</c> nhưng chỉ commit khi chính nó mở giao dịch.
    /// Không kiểm tra quyền và không ghi nhật ký riêng: bên gọi đã kiểm tra quyền sửa, hủy phiếu thu hoạch
    /// và đã ghi nhật ký <c>HarvestRecord.Correct</c> hoặc <c>HarvestRecord.Void</c>.
    /// Không ném lỗi nghiệp vụ.
    /// </remarks>
    Task<IReadOnlyList<Guid>> RemoveByHarvestRecordAsync(Guid harvestRecordId, CancellationToken ct);
}
