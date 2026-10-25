using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

// Doanh thu, đã thu, công nợ, trạng thái thanh toán không lưu, tính từ Quantity, UnitPrice và các lần thanh toán.
public class Sale : IHouseholdOwned, IAuditable, IVersioned, IVoidable, IRequestIdentified
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    // BR-SAL-01: có ít nhất một trong hai CustomerId, Channel.
    public Guid? CustomerId { get; set; }

    public SalesChannel? Channel { get; set; }

    public string? ChannelOther { get; set; }

    public QualityGrade QualityGrade { get; set; }

    // Theo đơn vị sản lượng của cây.
    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public DateOnly SaleDate { get; set; }

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

    public Customer? Customer { get; set; }

    public List<Payment> Payments { get; set; } = [];

    public List<SaleHarvestLink> HarvestLinks { get; set; } = [];
}
