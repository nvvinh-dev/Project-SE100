using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class ProduceStockAdjustmentConfiguration : IEntityTypeConfiguration<ProduceStockAdjustment>
{
    public void Configure(EntityTypeBuilder<ProduceStockAdjustment> builder)
    {
        builder.HasOne(a => a.Season).WithMany().HasForeignKey(a => a.SeasonId);

        builder.Property(a => a.QualityGrade).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.Quantity).HasPrecision(18, 3);
        builder.Property(a => a.Reason).HasConversion<string>().HasMaxLength(50);
        builder.Property(a => a.ReasonOther).HasMaxLength(200);

        // BR-GEN-10: lớp bảo vệ cuối chống tạo trùng khi yêu cầu bị gửi lại.
        builder.HasIndex(a => new { a.HouseholdId, a.RequestId }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_produce_stock_adjustments_quantity", "quantity > 0"));
    }
}
