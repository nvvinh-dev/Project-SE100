namespace HouseholdFarm.Api.Domain.Common;

// Bản ghi giao dịch không xóa được, ghi nhầm thì Void (BR-GEN-09). Bản ghi có VoidedAt bị loại khỏi mọi phép tính tổng.
public interface IVoidable
{
    DateTime? VoidedAt { get; set; }

    Guid? VoidedById { get; set; }

    string? VoidReason { get; set; }
}
