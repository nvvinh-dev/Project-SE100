using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SeasonAssignmentConfiguration : IEntityTypeConfiguration<SeasonAssignment>
{
    public void Configure(EntityTypeBuilder<SeasonAssignment> builder)
    {
        builder.HasOne(a => a.Season).WithMany(s => s.Assignments).HasForeignKey(a => a.SeasonId);
        builder.HasOne(a => a.User).WithMany().HasForeignKey(a => a.UserId);

        builder.HasIndex(a => new { a.SeasonId, a.UserId }).IsUnique();
    }
}
