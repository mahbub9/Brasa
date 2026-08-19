using Brasa.Modules.Payments.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Payments.Persistence;

internal sealed class CashMovementConfiguration : IEntityTypeConfiguration<CashMovement>
{
    public void Configure(EntityTypeBuilder<CashMovement> builder)
    {
        builder.ToTable("cash_movements");
        builder.ApplyEntityConventions();

        builder.Property(m => m.CashSessionId).IsRequired();
        builder.Property(m => m.Direction).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Reason).HasMaxLength(300).IsRequired();
        builder.Property(m => m.RecordedByStaffId).IsRequired();
        builder.Property(m => m.RecordedAtUtc).IsRequired();

        builder.MapMoney(m => m.Amount, "amount");

        builder.HasIndex(m => m.CashSessionId);
    }
}
