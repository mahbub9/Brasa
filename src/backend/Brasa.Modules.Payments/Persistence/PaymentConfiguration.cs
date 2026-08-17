using Brasa.Modules.Payments.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Payments.Persistence;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.ApplyEntityConventions();

        builder.Property(p => p.OrderId).IsRequired();
        builder.Property(p => p.Method).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(p => p.PaidAtUtc).IsRequired();

        // AmountApplied/Change/RemainingBalance are all derived from
        // AmountDue and AmountTendered and must never be persisted
        // separately — the same "never store a derived total" rule
        // OrderConfiguration already applies to Order.Total, so a stored
        // value could never silently drift from the figures that produce it.
        builder.Ignore(p => p.AmountApplied);
        builder.Ignore(p => p.Change);
        builder.Ignore(p => p.RemainingBalance);

        builder.MapMoney(p => p.AmountDue, "amount_due");
        builder.MapMoney(p => p.AmountTendered, "amount_tendered");
        builder.MapMoney(p => p.TipAmount, "tip_amount");

        builder.Property(p => p.AttributedStaffId).IsRequired(false);

        builder.HasIndex(p => p.OrderId);
    }
}
