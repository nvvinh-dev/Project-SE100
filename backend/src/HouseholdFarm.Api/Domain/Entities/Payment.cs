using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

// BR-SAL-07: thanh toán chỉ có Void, không có Correct.
public class Payment : IHouseholdOwned, IAuditable, IVersioned, IVoidable, IRequestIdentified
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SaleId { get; set; }

    public decimal Amount { get; set; }

    public DateOnly PaymentDate { get; set; }

    public string? Note { get; set; }

    // BR-GEN-10: chống tạo trùng khi yêu cầu bị gửi lại.
    public Guid RequestId { get; set; }

    public DateTime? VoidedAt { get; set; }

    public Guid? VoidedById { get; set; }

    public string? VoidReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public Sale Sale { get; set; } = null!;
}
