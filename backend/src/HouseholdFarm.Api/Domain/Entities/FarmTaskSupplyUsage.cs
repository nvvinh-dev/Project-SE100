using HouseholdFarm.Api.Domain.Common;

namespace HouseholdFarm.Api.Domain.Entities;

public class FarmTaskSupplyUsage : IHouseholdOwned, IAuditable, IVersioned
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid HouseholdId { get; set; }

    public Guid FarmTaskId { get; set; }

    public Guid SupplyId { get; set; }

    public decimal? PlannedQuantity { get; set; }

    public decimal? ActualQuantity { get; set; }

    // BR-TSK-08: giao dịch xuất kho sinh từ lượng thực dùng; đã có giá trị thì gửi lại cùng lượng không tạo giao dịch mới.
    public Guid? StockTransactionId { get; set; }

    public DateTime CreatedAt { get; set; }

    public Guid CreatedById { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public Guid? UpdatedById { get; set; }

    public uint Version { get; set; }

    public FarmTask FarmTask { get; set; } = null!;

    public Supply Supply { get; set; } = null!;

    public StockTransaction? StockTransaction { get; set; }
}
