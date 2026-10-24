using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class CropConfiguration : IEntityTypeConfiguration<Crop>
{
    public void Configure(EntityTypeBuilder<Crop> builder)
    {
        builder.Property(c => c.Name).HasMaxLength(200);
        builder.Property(c => c.Group).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.GroupOther).HasMaxLength(200);
        builder.Property(c => c.YieldUnit).HasConversion<string>().HasMaxLength(50);
        builder.Property(c => c.Status).HasConversion<string>().HasMaxLength(50);

        // BR-CAT-03: tên cây trồng duy nhất trong hộ.
        builder.HasIndex(c => new { c.HouseholdId, c.Name }).IsUnique();
    }
}
