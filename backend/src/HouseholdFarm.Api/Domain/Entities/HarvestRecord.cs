using HouseholdFarm.Api.Domain.Common;
using HouseholdFarm.Api.Domain.Enums;

namespace HouseholdFarm.Api.Domain.Entities;

public class HarvestRecord : IHouseholdOwned, IAuditable, IVersioned, IVoidable
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid SeasonId { get; set; }

    public DateOnly HarvestDate { get; set; }

    public QualityGrade QualityGrade { get; set; }

    // Đơn vị là đơn vị sản lượng của cây trong mùa vụ, không lưu ở bảng này.
    public decimal Quantity { get; set; }

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

    public List<SaleHarvestLink> SaleLinks { get; set; } = [];
}
