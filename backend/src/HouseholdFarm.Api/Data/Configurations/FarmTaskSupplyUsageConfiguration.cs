using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class FarmTaskSupplyUsageConfiguration : IEntityTypeConfiguration<FarmTaskSupplyUsage>
{
    public void Configure(EntityTypeBuilder<FarmTaskSupplyUsage> builder)
    {
        builder.HasOne(u => u.FarmTask).WithMany(t => t.SupplyUsages).HasForeignKey(u => u.FarmTaskId);
        builder.HasOne(u => u.Supply).WithMany().HasForeignKey(u => u.SupplyId);
        // BR-TSK-08: một giao dịch xuất chỉ sinh từ đúng một dòng vật tư của công việc.
        builder.HasOne(u => u.StockTransaction).WithOne().HasForeignKey<FarmTaskSupplyUsage>(u => u.StockTransactionId);

        builder.Property(u => u.PlannedQuantity).HasPrecision(18, 3);
        builder.Property(u => u.ActualQuantity).HasPrecision(18, 3);

        builder.HasIndex(u => new { u.FarmTaskId, u.SupplyId }).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "ck_farm_task_supply_usages_planned_quantity",
                "planned_quantity IS NULL OR planned_quantity > 0");
            t.HasCheckConstraint(
                "ck_farm_task_supply_usages_actual_quantity",
                "actual_quantity IS NULL OR actual_quantity > 0");
        });
    }
}
