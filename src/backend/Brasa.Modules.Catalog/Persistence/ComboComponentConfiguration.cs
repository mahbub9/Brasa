using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class ComboComponentConfiguration : IEntityTypeConfiguration<ComboComponent>
{
    public void Configure(EntityTypeBuilder<ComboComponent> builder)
    {
        builder.ToTable("combo_components");
        builder.ApplyEntityConventions();

        builder.Property(cc => cc.ComboId).IsRequired();
        builder.Property(cc => cc.MenuItemId).IsRequired();

        // Defence in depth alongside Combo.AddComponent's own in-memory
        // duplicate check — the same shape PriceListEntryConfiguration's
        // (PriceListId, MenuItemId) index already uses.
        builder.HasIndex(cc => new { cc.ComboId, cc.MenuItemId }).IsUnique();
    }
}
