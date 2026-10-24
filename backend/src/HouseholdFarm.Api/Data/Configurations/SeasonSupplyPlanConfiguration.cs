using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SeasonSupplyPlanConfiguration : IEntityTypeConfiguration<SeasonSupplyPlan>
{
    public void Configure(EntityTypeBuilder<SeasonSupplyPlan> builder)
    {
        builder.HasOne(p => p.Season).WithMany(s => s.SupplyPlans).HasForeignKey(p => p.SeasonId);
        builder.HasOne(p => p.Supply).WithMany().HasForeignKey(p => p.SupplyId);

        builder.Property(p => p.PlannedQuantity).HasPrecision(18, 3);

        builder.HasIndex(p => new { p.SeasonId, p.SupplyId }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_season_supply_plans_planned_quantity", "planned_quantity > 0"));
    }
}
