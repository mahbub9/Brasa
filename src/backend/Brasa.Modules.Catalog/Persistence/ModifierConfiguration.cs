using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class ModifierConfiguration : IEntityTypeConfiguration<Modifier>
{
    public void Configure(EntityTypeBuilder<Modifier> builder)
    {
        builder.ToTable("modifiers");
        builder.ApplyEntityConventions();

        builder.Property(m => m.ModifierGroupId).IsRequired();
        builder.Property(m => m.Name).HasMaxLength(200).IsRequired();
        builder.Property(m => m.DisplayOrder).IsRequired();

        builder.MapMoney(m => m.PriceDelta, "price_delta");

        builder.HasIndex(m => m.ModifierGroupId);
    }
}
