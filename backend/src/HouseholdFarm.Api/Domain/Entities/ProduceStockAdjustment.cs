using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class ProduceStockAdjustment : IHouseholdOwned, IAuditable, IVersioned, IVoidable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    public QualityGrade QualityGrade { get; set; }

    // Lượng giảm, luôn dương.
    public decimal Quantity { get; set; }

    public DateOnly AdjustmentDate { get; set; }

    public ProduceAdjustmentReason Reason { get; set; }

    public string? ReasonOther { get; set; }

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

    public Season Season { get; set; } = null!;
}
