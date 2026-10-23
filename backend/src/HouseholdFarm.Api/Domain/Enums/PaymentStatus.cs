namespace HouseholdFarm.Api.Domain.Enums;

// Giá trị tính từ tổng thanh toán so với doanh thu, không lưu trong database.
public enum PaymentStatus
{
    Unpaid,
    PartiallyPaid,
    Paid,
}
