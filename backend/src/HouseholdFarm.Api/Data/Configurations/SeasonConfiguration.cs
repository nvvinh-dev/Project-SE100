using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SeasonConfiguration : IEntityTypeConfiguration<Season>
{
    public void Configure(EntityTypeBuilder<Season> builder)
    {
        builder.HasOne(s => s.Plot).WithMany().HasForeignKey(s => s.PlotId);
        builder.HasOne(s => s.Crop).WithMany().HasForeignKey(s => s.CropId);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.ClosedById);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(s => s.CancelledById);

        builder.Property(s => s.TargetYield).HasPrecision(18, 3);
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(50);

        builder.ToTable(t =>
        {
            // BR-SEA-01
            t.HasCheckConstraint("ck_seasons_expected_harvest_date", "expected_harvest_date >= start_date");
            t.HasCheckConstraint("ck_seasons_target_yield", "target_yield > 0");

            // BR-GEN-06: hủy mùa vụ bắt buộc lý do.
            t.HasCheckConstraint("ck_seasons_cancel_reason", "cancelled_at IS NULL OR cancel_reason IS NOT NULL");
        });
    }
}
