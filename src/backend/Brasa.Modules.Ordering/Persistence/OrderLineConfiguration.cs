using Brasa.Modules.Ordering.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Ordering.Persistence;

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("order_lines");
        builder.ApplyEntityConventions();

        builder.Property(l => l.OrderId).IsRequired();
        builder.Property(l => l.MenuItemId).IsRequired();
        builder.Property(l => l.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(l => l.VatRateFraction).HasColumnType("numeric(4,2)").IsRequired();
        builder.Property(l => l.Quantity).IsRequired();
        builder.Property(l => l.Notes).HasMaxLength(300);

        builder.MapMoney(l => l.UnitPrice, "unit_price");

        // LineTotal and ModifiersTotal are derived and never persisted.
        builder.Ignore(l => l.LineTotal);
        builder.Ignore(l => l.ModifiersTotal);

        builder.HasMany(l => l.Modifiers)
            .WithOne()
            .HasForeignKey(m => m.OrderLineId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(l => l.Modifiers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(l => l.OrderId);
    }
}
