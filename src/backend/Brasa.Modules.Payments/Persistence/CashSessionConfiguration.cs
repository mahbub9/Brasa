using Brasa.Modules.Payments.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Payments.Persistence;

internal sealed class CashSessionConfiguration : IEntityTypeConfiguration<CashSession>
{
    public void Configure(EntityTypeBuilder<CashSession> builder)
    {
        builder.ToTable("cash_sessions");
        builder.ApplyEntityConventions();

        builder.Property(c => c.TerminalId).IsRequired();
        builder.Property(c => c.OpenedByStaffId).IsRequired();
        builder.Property(c => c.OpenedAtUtc).IsRequired();
        builder.Property(c => c.ClosedAtUtc).IsRequired(false);

        // Computed from ClosedAtUtc — never a stored column, the same
        // "never store a derived total" rule Payment.AmountApplied follows.
        builder.Ignore(c => c.IsOpen);

        builder.MapMoney(c => c.OpeningFloat, "opening_float");

        builder.HasIndex(c => c.TerminalId);
    }
}
