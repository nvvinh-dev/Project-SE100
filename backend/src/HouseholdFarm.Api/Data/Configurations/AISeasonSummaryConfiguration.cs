using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class AISeasonSummaryConfiguration : IEntityTypeConfiguration<AISeasonSummary>
{
    public void Configure(EntityTypeBuilder<AISeasonSummary> builder)
    {
        builder.HasOne(s => s.Season).WithMany().HasForeignKey(s => s.SeasonId);

        builder.Property(s => s.Model).HasMaxLength(100);
    }
}
