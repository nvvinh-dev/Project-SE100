namespace HouseholdFarm.Api.Features.Sales;

/// <summary>Ghi một lần khách trả tiền cho giao dịch bán (FR-SAL-04). Thanh toán không có <c>correct</c> (BR-SAL-07).</summary>
/// <param name="RequestId">Sinh một lần khi mở hộp thoại, chống ghi trùng khi gửi lại (BR-GEN-10).</param>
/// <param name="PaymentDate">Được trước ngày bán (tiền cọc), không được sau hôm nay (BR-SAL-06).</param>
public sealed record CreatePaymentRequest(
    Guid RequestId,
    Guid? SaleId,
    decimal Amount,
    DateOnly? PaymentDate,
    string? Note);

/// <summary>Hủy thanh toán ghi nhầm, bắt buộc lý do (BR-GEN-06).</summary>
public sealed record VoidPaymentRequest(string Reason);

/// <summary>Một lần thanh toán của giao dịch bán.</summary>
public sealed record PaymentResponse(
    Guid Id,
    Guid SaleId,
    decimal Amount,
    DateOnly PaymentDate,
    string? Note,
    DateTime? VoidedAt,
    string? VoidReason,
    DateTime CreatedAt);
