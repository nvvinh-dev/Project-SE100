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
    /// <returns>
    /// Danh sách luôn đủ 3 dòng cho 3 loại chất lượng theo thứ tự enum (<see cref="QualityGrade.Grade1"/>,
    /// <see cref="QualityGrade.Grade2"/>, <see cref="QualityGrade.Grade3"/>). Loại chưa có thu hoạch hoặc đã hết thì tồn bằng 0.
    /// </returns>
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
    /// <returns>
    /// Danh sách tồn nông sản theo mùa vụ và loại chất lượng. Mỗi mùa vụ trả đủ 3 dòng theo từng loại chất lượng;
    /// kết quả không đảm bảo thứ tự mùa vụ, bên gọi tra theo <c>SeasonId</c> và <c>QualityGrade</c>.
    /// </returns>
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
    /// Id của giao dịch bán đang điều chỉnh (BR-SAL-03); truyền <c>null</c> khi tạo mới giao dịch bán (BR-SAL-01).
    /// Tồn khả dụng của cặp (<paramref name="seasonId"/>, <paramref name="qualityGrade"/>) được tính không kể
    /// giao dịch <paramref name="currentSaleId"/> trong tổng đã bán; cách tính này bảo đảm chính xác cả khi giữ nguyên
    /// lẫn khi đổi loại chất lượng lúc điều chỉnh.
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <remarks>
    /// <para>
    /// Không kiểm tra quyền và phạm vi: bên gọi đã kiểm tra qua <c>ISeasonStateGuard</c> trước khi gọi hàm này.
    /// Bên gọi (module Sales) phải mở giao dịch database và khóa dòng <c>seasons</c> trước khi gọi hàm này (BR-GEN-12).
    /// Hàm này tham gia giao dịch database của bên gọi, đọc dữ liệu thu hoạch, các giao dịch bán khác và điều chỉnh giảm tồn
    /// để tính tồn khả dụng. Không tự commit.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ;
    /// <c>INSUFFICIENT_PRODUCE_STOCK</c> khi lượng bán lớn hơn tồn khả dụng của cặp (mùa vụ, loại chất lượng).
    /// Tồn của các loại chất lượng khác không được bù trừ (BR-HAR-02, TC-12; TC-11 khi bán vượt tồn cùng loại);
    /// lỗi trả kèm <c>details = { seasonId, qualityGrade, available, requested }</c>.
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
    /// Không kiểm tra quyền và phạm vi: bên gọi đã kiểm tra qua <c>ISeasonStateGuard</c> trước khi gọi hàm này.
    /// Mùa vụ mất trắng (chưa từng có phiếu thu hoạch) vẫn thỏa mãn điều kiện tồn bằng 0 để chốt (BR-SEA-05).
    /// Tham gia giao dịch database của bên gọi (bên gọi là <c>SeasonService.CloseAsync</c> đã khóa dòng <c>seasons</c> trước).
    /// Không tự mở và không tự commit giao dịch.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi mùa vụ không tồn tại trong hộ;
    /// <c>SEASON_PRODUCE_REMAINING</c> khi mùa vụ vẫn còn tồn nông sản lớn hơn 0 ở bất kỳ loại chất lượng nào;
    /// lỗi trả kèm <c>details = { seasonId, remainingStock }</c> danh sách các loại chất lượng còn tồn.
    /// </para>
    /// </remarks>
    Task EnsureZeroStockAsync(Guid seasonId, CancellationToken ct);

    /// <summary>
    /// Kiểm tra danh sách phiếu thu hoạch hợp lệ để liên kết với giao dịch bán (BR-SAL-05).
    /// Phiếu phải cùng mùa vụ, cùng loại chất lượng và chưa bị hủy (<c>Void</c>).
    /// </summary>
    /// <param name="seasonId">Mùa vụ của giao dịch bán.</param>
    /// <param name="qualityGrade">Loại chất lượng của giao dịch bán.</param>
    /// <param name="harvestRecordIds">
    /// Danh sách Id phiếu thu hoạch muốn liên kết. Tự động loại bỏ Id trùng lặp trước khi kiểm tra;
    /// danh sách rỗng thì không làm gì (hợp lệ).
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <remarks>
    /// <para>
    /// Không kiểm tra quyền và phạm vi: bên gọi đã kiểm tra qua <c>ISeasonStateGuard</c> trước khi gọi hàm này.
    /// Liên kết phiếu thu là tùy chọn, không ràng buộc số lượng thu so với lượng bán (chỉ để tham khảo).
    /// Tham gia giao dịch database của bên gọi nếu có; chỉ đọc dữ liệu để kiểm tra.
    /// </para>
    /// <para>
    /// Lỗi: <c>VALIDATION_ERROR</c> khi có phiếu không thuộc hộ, khác mùa vụ, khác loại chất lượng, hoặc đã bị Void;
    /// lỗi trả kèm <c>details = { invalidHarvestRecordIds }</c> chứa danh sách Id các phiếu không hợp lệ.
    /// </para>
    /// </remarks>
    Task ValidateHarvestLinksForSaleAsync(
        Guid seasonId,
        QualityGrade qualityGrade,
        IReadOnlyCollection<Guid> harvestRecordIds,
        CancellationToken ct);
}
