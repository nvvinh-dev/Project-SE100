using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class PlotConfiguration : IEntityTypeConfiguration<Plot>
{
    public void Configure(EntityTypeBuilder<Plot> builder)
    {
        builder.Property(p => p.Code).HasMaxLength(20);
        builder.Property(p => p.AreaM2).HasPrecision(18, 2);
        builder.Property(p => p.SoilType).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.SoilTypeOther).HasMaxLength(200);
        builder.Property(p => p.WaterSource).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.WaterSourceOther).HasMaxLength(200);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(50);

        // BR-CAT-01: mã thửa duy nhất trong hộ.
        builder.HasIndex(p => new { p.HouseholdId, p.Code }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_plots_area_m2", "area_m2 > 0"));
    }
}
