using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class StockTransactionConfiguration : IEntityTypeConfiguration<StockTransaction>
{
    public void Configure(EntityTypeBuilder<StockTransaction> builder)
    {
        builder.HasOne(t => t.Supply).WithMany().HasForeignKey(t => t.SupplyId);
        builder.HasOne(t => t.Season).WithMany().HasForeignKey(t => t.SeasonId);

        builder.Property(t => t.Type).HasConversion<string>().HasMaxLength(50);
        builder.Property(t => t.Quantity).HasPrecision(18, 3);
        builder.Property(t => t.UnitCost).HasPrecision(18, 4);
        builder.Property(t => t.AverageUnitCostBefore).HasPrecision(18, 4);
        builder.Property(t => t.TotalCost).HasPrecision(18, 2);

        // BR-GEN-10: lớp bảo vệ cuối chống tạo trùng khi yêu cầu bị gửi lại.
        builder.HasIndex(t => new { t.HouseholdId, t.RequestId }).IsUnique();

        // Dấu của quantity theo loại giao dịch là điều kiện để tồn kho chỉ cần cộng tổng, nên chặn cả ở database.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_stock_transactions_in", "type <> 'In' OR (quantity > 0 AND season_id IS NULL)");
            t.HasCheckConstraint("ck_stock_transactions_out", "type <> 'Out' OR (quantity < 0 AND season_id IS NOT NULL)");
            t.HasCheckConstraint(
                "ck_stock_transactions_adjustment",
                "type <> 'Adjustment' OR (quantity <> 0 AND season_id IS NULL AND reason IS NOT NULL)");
            t.HasCheckConstraint("ck_stock_transactions_unit_cost", "unit_cost >= 0");
            t.HasCheckConstraint("ck_stock_transactions_total_cost", "total_cost >= 0");
        });
    }
}
