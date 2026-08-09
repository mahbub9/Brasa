using Brasa.Modules.Ordering.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Ordering.Persistence;

internal sealed class OrderLineModifierConfiguration : IEntityTypeConfiguration<OrderLineModifier>
{
    public void Configure(EntityTypeBuilder<OrderLineModifier> builder)
    {
        builder.ToTable("order_line_modifiers");
        builder.ApplyEntityConventions();

        builder.Property(m => m.OrderLineId).IsRequired();
        builder.Property(m => m.ModifierId).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();

        builder.MapMoney(m => m.PriceDelta, "price_delta");

        builder.HasIndex(m => m.OrderLineId);
    }
}
