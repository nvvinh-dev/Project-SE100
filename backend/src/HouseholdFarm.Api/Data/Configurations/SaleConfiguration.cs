using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SaleConfiguration : IEntityTypeConfiguration<Sale>
{
    public void Configure(EntityTypeBuilder<Sale> builder)
    {
        builder.HasOne(s => s.Season).WithMany().HasForeignKey(s => s.SeasonId);
        builder.HasOne(s => s.Customer).WithMany().HasForeignKey(s => s.CustomerId);

        builder.Property(s => s.Channel).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.ChannelOther).HasMaxLength(200);
        builder.Property(s => s.QualityGrade).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Quantity).HasPrecision(18, 3);
        builder.Property(s => s.UnitPrice).HasPrecision(18, 4);

        // BR-GEN-10: lớp bảo vệ cuối chống tạo trùng khi yêu cầu bị gửi lại.
        builder.HasIndex(s => new { s.HouseholdId, s.RequestId }).IsUnique();

        // BR-SAL-01
        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_sales_quantity", "quantity > 0");
            t.HasCheckConstraint("ck_sales_unit_price", "unit_price >= 0");
            t.HasCheckConstraint("ck_sales_customer_or_channel", "customer_id IS NOT NULL OR channel IS NOT NULL");
        });
    }
}
