using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

// Bảng nối nhiều-nhiều, khóa ghép (SaleId, HarvestRecordId). Liên kết chỉ để tham khảo, không ràng buộc số lượng (BR-SAL-05).
public class SaleHarvestLink : IHouseholdOwned, IAuditable
{
    public Guid SaleId { get; set; }

    public Guid HarvestRecordId { get; set; }

    public Guid HouseholdId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public Sale Sale { get; set; } = null!;

    public HarvestRecord HarvestRecord { get; set; } = null!;
}
