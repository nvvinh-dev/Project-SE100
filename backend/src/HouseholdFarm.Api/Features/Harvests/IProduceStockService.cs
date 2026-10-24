using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Features.Harvests;

/// <summary>
/// Điểm nối tồn nông sản và phiếu thu hoạch cho module khác: bán hàng kiểm tra tồn và liên kết phiếu thu
/// khi tạo, điều chỉnh giao dịch bán; mùa vụ kiểm tra tồn khi chốt mùa vụ.
/// </summary>
/// <remarks>
/// Tồn nông sản được tính toán từ dữ liệu (tổng thu hoạch − tổng đã bán − tổng điều chỉnh giảm), không lưu cứng (FR-HAR-05).
/// Các hàm kiểm tra tham gia vào giao dịch database của bên gọi nếu đã có (không mở giao dịch mới).
/// </remarks>
public interface IProduceStockService
{
    /// <summary>
    /// Lấy danh sách tồn nông sản hiện có theo từng loại chất lượng của một mùa vụ (FR-HAR-05, FR-HAR-06).
    /// </summary>
    /// <param name="seasonId">Id của mùa vụ cần xem tồn nông sản.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Danh sách tồn nông sản theo từng loại chất lượng (Grade1, Grade2, Grade3) của mùa vụ.</returns>
    /// <remarks>
    /// <para>
    /// Chỉ đọc dữ liệu, không thay đổi database. Tham gia giao dịch database của bên gọi nếu có.
    /// Tự kiểm tra quyền xem tồn nông sản (BR-USR-13): Chủ hộ, Thành viên trong phân công mùa vụ,
    /// hoặc Thành viên có quyền Quản lý tài chính (<c>ManageFinance</c>) được xem mọi mùa vụ của hộ.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ hoặc nằm ngoài phạm vi người dùng (BR-GEN-02);
    /// <c>FORBIDDEN</c> khi người dùng là Nhân công.
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// Lấy tồn nông sản theo từng loại chất lượng của nhiều mùa vụ (dùng cho danh sách tồn, báo cáo).
    /// </summary>
    /// <param name="seasonIds">Danh sách Id mùa vụ; Id trùng chỉ trả một lần, danh sách rỗng trả danh sách rỗng.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Danh sách tồn nông sản theo mùa vụ và loại chất lượng.</returns>
    /// <remarks>
    /// <para>
    /// Chỉ đọc dữ liệu, không thay đổi database. Tham gia giao dịch database của bên gọi nếu có.
    /// Lỗi: <c>FORBIDDEN</c> khi người dùng là Nhân công;
    /// <c>NOT_FOUND</c> khi có mùa vụ không thuộc hộ hoặc ngoài phạm vi được phép (BR-GEN-02, BR-USR-13).
    /// </para>
    /// </remarks>
    Task<IReadOnlyList<ProduceStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> seasonIds, CancellationToken ct);

    /// <summary>
    /// Kiểm tra đủ tồn nông sản khi tạo hoặc điều chỉnh giao dịch bán (BR-SAL-01, BR-SAL-03).
    /// </summary>
    /// <param name="seasonId">Id mùa vụ bán nông sản.</param>
    /// <param name="qualityGrade">Loại chất lượng nông sản bán.</param>
    /// <param name="quantity">Lượng bán cần kiểm tra (lớn hơn 0).</param>
    /// <param name="currentSaleId">
    /// Id của giao dịch bán đang điều chỉnh (BR-SAL-03) để hoàn lại lượng của chính giao dịch này trước khi tính tồn;
    /// truyền <c>null</c> khi tạo mới giao dịch bán (BR-SAL-01).
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <remarks>
    /// <para>
    /// Bên gọi (module Sales) phải mở giao dịch database và khóa dòng <c>seasons</c> trước khi gọi hàm này (BR-GEN-12).
    /// Hàm này tham gia giao dịch database của bên gọi, đọc dữ liệu thu hoạch, các giao dịch bán khác và điều chỉnh giảm tồn
    /// để tính tồn khả dụng. Không tự commit.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ hoặc ngoài phạm vi;
    /// <c>INSUFFICIENT_PRODUCE_STOCK</c> khi lượng bán lớn hơn tồn nông sản của cặp (mùa vụ, loại chất lượng).
    /// Tồn của các loại chất lượng khác không được bù trừ (BR-SAL-01, TC-11).
    /// </para>
    /// </remarks>
    Task EnsureSufficientStockForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        decimal quantity,
        Guid? currentSaleId,
        CancellationToken ct);

    /// <summary>
    /// Kiểm tra tồn mọi loại chất lượng của mùa vụ bằng 0 khi chốt mùa vụ (BR-SEA-05, TC-15).
    /// </summary>
    /// <param name="seasonId">Id mùa vụ cần chốt.</param>
    /// <param name="ct">Token hủy.</param>
    /// <remarks>
    /// <para>
    /// Mùa vụ mất trắng (chưa từng có phiếu thu hoạch) vẫn thỏa mãn điều kiện tồn bằng 0 để chốt (BR-SEA-05).
    /// Tham gia giao dịch database của bên gọi (bên gọi là <c>SeasonService.CloseAsync</c> đã khóa dòng <c>seasons</c> trước).
    /// Không tự mở và không tự commit giao dịch.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ;
    /// <c>SEASON_PRODUCE_REMAINING</c> khi mùa vụ vẫn còn tồn nông sản lớn hơn 0 ở bất kỳ loại chất lượng nào.
    /// </para>
    /// </remarks>
    Task EnsureZeroStockAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// Kiểm tra danh sách phiếu thu hoạch hợp lệ để liên kết với giao dịch bán (BR-SAL-05).
    /// Phiếu phải cùng mùa vụ, cùng loại chất lượng và chưa bị hủy (<c>Void</c>).
    /// </summary>
    /// <param name="seasonId">Mùa vụ của giao dịch bán.</param>
    /// <param name="qualityGrade">Loại chất lượng của giao dịch bán.</param>
    /// <param name="harvestRecordIds">Danh sách Id phiếu thu hoạch muốn liên kết. Danh sách rỗng thì không làm gì (hợp lệ).</param>
    /// <param name="ct">Token hủy.</param>
    /// <remarks>
    /// <para>
    /// Liên kết phiếu thu là tùy chọn, không ràng buộc số lượng thu so với lượng bán (chỉ để tham khảo).
    /// Tham gia giao dịch database của bên gọi nếu có; chỉ đọc dữ liệu để kiểm tra.
    /// </para>
    /// <para>
    /// Lỗi: <c>VALIDATION_ERROR</c> khi có phiếu không thuộc hộ, khác mùa vụ, khác loại chất lượng, hoặc đã bị Void.
    /// </para>
    /// </remarks>
    Task ValidateHarvestLinksForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        IReadOnlyCollection<Guid> harvestRecordIds,
        CancellationToken ct);
}

