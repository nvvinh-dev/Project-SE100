using HouseholdFarm.Api.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HouseholdFarm.Api.Data.Configurations;

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.HasOne(p => p.Sale).WithMany(s => s.Payments).HasForeignKey(p => p.SaleId);

        builder.Property(p => p.Amount).HasPrecision(18, 2);

        // BR-GEN-10: lớp bảo vệ cuối chống tạo trùng khi yêu cầu bị gửi lại.
        builder.HasIndex(p => new { p.HouseholdId, p.RequestId }).IsUnique();

        builder.ToTable(t => t.HasCheckConstraint("ck_payments_amount", "amount > 0"));
    }
}
