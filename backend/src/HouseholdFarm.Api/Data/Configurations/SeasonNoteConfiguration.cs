using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class SeasonNoteConfiguration : IEntityTypeConfiguration<SeasonNote>
{
    public void Configure(EntityTypeBuilder<SeasonNote> builder)
    {
        builder.HasOne(n => n.Season).WithMany(s => s.Notes).HasForeignKey(n => n.SeasonId);
    }
}
