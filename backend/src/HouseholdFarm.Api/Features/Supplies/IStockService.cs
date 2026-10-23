namespace HouseholdFarm.Api.Features.Supplies;

/// <summary>
/// Điểm nối kho vật tư cho module khác: mùa vụ kiểm tra tồn khi khai báo vật tư dự kiến,
/// công việc xuất kho từ vật tư đã dùng.
/// </summary>
/// <remarks>
/// Hàm ghi dùng giao dịch database của bên gọi nếu đã có, không mở giao dịch mới; chưa có thì tự mở và tự commit.
/// Hàm ghi tự gọi <c>SaveChangesAsync</c> để lần gọi sau trong cùng giao dịch tính đúng tồn,
/// nhưng chỉ commit khi chính nó mở giao dịch.
/// </remarks>
public interface IStockService
{
    /// <summary>
    /// Lấy tồn hiện tại và ngưỡng tồn tối thiểu của các vật tư, dùng khi khai báo vật tư dự kiến của mùa vụ
    /// (FR-SEA-04, BR-SEA-09). Tồn tính từ các giao dịch kho chưa Void (BR-SUP-01). Không trả giá.
    /// </summary>
    /// <param name="supplyIds">Id các vật tư cần lấy; id trùng chỉ trả một dòng, danh sách rỗng trả danh sách rỗng.</param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Mỗi vật tư một dòng, không đảm bảo thứ tự; bên gọi tra theo <c>SupplyId</c>.</returns>
    /// <remarks>
    /// Lỗi: <c>FORBIDDEN</c> khi người dùng là Nhân công (không được xem tồn kho);
    /// <c>NOT_FOUND</c> khi có vật tư không thuộc hộ hiện tại.
    /// </remarks>
    Task<IReadOnlyList<SupplyStockResponse>> GetStockAsync(IReadOnlyCollection<Guid> supplyIds, CancellationToken ct);

    /// <summary>
    /// Tạo giao dịch <c>Out</c> cho mùa vụ của công việc từ lượng thực dùng của một dòng vật tư
    /// (FR-TSK-05, BR-TSK-08, BR-SUP-06, BR-SUP-07, BR-SUP-08).
    /// </summary>
    /// <param name="farmTaskId">Công việc có dòng vật tư; giao dịch gắn mùa vụ của công việc này.</param>
    /// <param name="supplyId">Vật tư được xuất.</param>
    /// <param name="quantity">Lượng thực dùng, lớn hơn 0, theo đơn vị của vật tư; giao dịch lưu thành số âm.</param>
    /// <param name="performedDate">
    /// Ngày thực hiện của công việc; để trống thì lấy hôm nay theo giờ Việt Nam (BR-TSK-08).
    /// </param>
    /// <param name="ct">Token hủy.</param>
    /// <returns>Giao dịch vừa tạo; bên gọi gán <c>Id</c> vào <c>stock_transaction_id</c> của dòng vật tư.</returns>
    /// <remarks>
    /// <para>
    /// Không kiểm tra quyền Quản lý kho: quyền ghi vật tư đã dùng do công việc kiểm tra, và bên gọi không đưa
    /// đơn giá, giá trị ra API cho người không có quyền này. Không kiểm tra vật tư <c>Inactive</c>: công việc đã
    /// kiểm tra BR-GEN-13 khi thêm dòng vật tư.
    /// </para>
    /// <para>
    /// Tự sinh <c>request_id</c>; chống ghi trùng là việc của bên gọi, dựa vào <c>stock_transaction_id</c> của dòng vật tư.
    /// Khóa dòng <c>supplies</c> của vật tư (BR-GEN-12): một lần ghi có nhiều vật tư thì gọi theo thứ tự
    /// <c>supplyId</c> tăng dần để hai yêu cầu đồng thời không khóa chéo nhau.
    /// Ghi nhật ký <c>StockTransaction.StockOut</c>; ghi cảnh báo <c>LOW_STOCK</c>, <c>NEGATIVE_STOCK</c>
    /// qua <c>IWarningCollector</c>.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi công việc hoặc vật tư không thuộc hộ hiện tại;
    /// <c>SEASON_INVALID_STATE</c> khi mùa vụ của công việc không ở trạng thái <c>InProgress</c>;
    /// <c>INSUFFICIENT_STOCK</c> khi tồn sau khi xuất nhỏ hơn 0 mà hộ không bật cho phép âm kho.
    /// </para>
    /// </remarks>
    Task<StockTransactionResponse> StockOutForFarmTaskAsync(
        Guid farmTaskId,
        Guid supplyId,
        decimal quantity,
        DateOnly? performedDate,
        CancellationToken ct);

    /// <summary>
    /// Hủy giao dịch <c>Out</c> sinh từ công việc khi sửa hoặc bỏ lượng thực dùng (BR-TSK-10). Lượng xuất được
    /// nhập lại kho với đơn giá của chính giao dịch đó và cập nhật giá vốn bình quân (BR-SUP-11).
    /// Đây là cách duy nhất hủy giao dịch xuất của công việc (BR-TSK-11).
    /// </summary>
    /// <param name="stockTransactionId">Giao dịch <c>Out</c> đang gắn với dòng vật tư của công việc.</param>
    /// <param name="reason">Lý do hủy, bắt buộc (BR-GEN-06).</param>
    /// <param name="ct">Token hủy.</param>
    /// <remarks>
    /// <para>
    /// Sửa lượng thực dùng thì gọi hàm này trước rồi mới gọi <see cref="StockOutForFarmTaskAsync"/>,
    /// để giao dịch mới được kiểm tra tồn sau khi đã hoàn lại lượng cũ.
    /// Không kiểm tra quyền Quản lý kho, cùng lý do như khi xuất. Khóa dòng <c>supplies</c> (BR-GEN-12);
    /// ghi nhật ký <c>StockTransaction.Void</c>.
    /// </para>
    /// <para>
    /// Lỗi: <c>NOT_FOUND</c> khi giao dịch không thuộc hộ hiện tại; <c>ALREADY_VOIDED</c> khi giao dịch đã bị hủy;
    /// <c>SEASON_INVALID_STATE</c> khi mùa vụ của giao dịch không ở trạng thái <c>InProgress</c>;
    /// <c>VALIDATION_ERROR</c> khi lý do rỗng.
    /// </para>
    /// </remarks>
    Task VoidFarmTaskStockOutAsync(Guid stockTransactionId, string reason, CancellationToken ct);
}
