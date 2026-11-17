namespace HouseholdFarm.Api.Features.Sales;

/// <summary>
/// Thanh toán của giao dịch bán (FR-SAL-04), do Chủ hộ, Thành viên hộ có quyền Quản lý tài chính thực hiện.
/// Ghi, hủy được cả khi mùa vụ đã chốt (mục 4.3). Chỉ có <c>Void</c>, không có <c>Correct</c> (BR-SAL-07).
/// </summary>
public interface IPaymentService
{
    /// <summary>
    /// Ghi thanh toán (BR-SAL-06). Gửi lại cùng <c>requestId</c> thì trả về thanh toán đã ghi (BR-GEN-10).
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>ALREADY_VOIDED</c>, <c>SEASON_INVALID_STATE</c>,
    /// <c>PAYMENT_EXCEEDS_OUTSTANDING</c>.
    /// </summary>
    Task<PaymentResponse> CreateAsync(CreatePaymentRequest request, CancellationToken ct);

    /// <summary>
    /// Hủy thanh toán ghi nhầm (BR-GEN-09); công nợ của giao dịch bán tăng lại.
    /// Lỗi: <c>FORBIDDEN</c>, <c>NOT_FOUND</c>, <c>ALREADY_VOIDED</c>, <c>SEASON_INVALID_STATE</c>.
    /// </summary>
    Task<PaymentResponse> VoidAsync(Guid id, VoidPaymentRequest request, CancellationToken ct);
}
