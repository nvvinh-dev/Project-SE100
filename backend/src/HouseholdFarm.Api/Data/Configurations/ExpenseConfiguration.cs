using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.HasOne(e => e.Season).WithMany().HasForeignKey(e => e.SeasonId);

        builder.Property(e => e.Category).HasConversion<string>().HasMaxLength(50);
        builder.Property(e => e.CategoryOther).HasMaxLength(200);
        builder.Property(e => e.Amount).HasPrecision(18, 2);

        // BR-GEN-10: lớp bảo vệ cuối chống tạo trùng khi yêu cầu bị gửi lại.
        builder.HasIndex(e => new { e.HouseholdId, e.RequestId }).IsUnique();

        // BR-EXP-01
        builder.ToTable(t => t.HasCheckConstraint("ck_expenses_amount", "amount > 0"));
    }
}
