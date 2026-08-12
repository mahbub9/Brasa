using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class PriceListEntryConfiguration : IEntityTypeConfiguration<PriceListEntry>
{
    public void Configure(EntityTypeBuilder<PriceListEntry> builder)
    {
        builder.ToTable("price_list_entries");
        builder.ApplyEntityConventions();

        builder.Property(e => e.PriceListId).IsRequired();
        builder.Property(e => e.MenuItemId).IsRequired();

        builder.MapMoney(e => e.Price, "price");

        // Defence in depth alongside PriceList.AddEntry's own in-memory
        // duplicate check — the same "domain guard plus a DB backstop"
        // shape TableConfiguration's xmin token uses for a different race.
        builder.HasIndex(e => new { e.PriceListId, e.MenuItemId }).IsUnique();
    }
}
