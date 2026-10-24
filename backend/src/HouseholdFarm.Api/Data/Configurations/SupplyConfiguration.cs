using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SupplyConfiguration : IEntityTypeConfiguration<Supply>
{
    public void Configure(EntityTypeBuilder<Supply> builder)
    {
        builder.Property(s => s.Code).HasMaxLength(20);
        builder.Property(s => s.Name).HasMaxLength(200);
        builder.Property(s => s.Group).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.Unit).HasConversion<string>().HasMaxLength(50);
        builder.Property(s => s.MinStockLevel).HasPrecision(18, 3);
        builder.Property(s => s.ReferencePrice).HasPrecision(18, 4);
        builder.Property(s => s.AverageUnitCost).HasPrecision(18, 4);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);

        // BR-CAT-01: mã vật tư duy nhất trong hộ.
        builder.HasIndex(s => new { s.HouseholdId, s.Code }).IsUnique();

        builder.ToTable(t =>
        {
            t.HasCheckConstraint("ck_supplies_min_stock_level", "min_stock_level >= 0");
            t.HasCheckConstraint("ck_supplies_average_unit_cost", "average_unit_cost >= 0");
        });
    }
}
