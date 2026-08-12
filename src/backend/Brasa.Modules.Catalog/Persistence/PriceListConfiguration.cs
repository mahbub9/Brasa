using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class PriceListConfiguration : IEntityTypeConfiguration<PriceList>
{
    public void Configure(EntityTypeBuilder<PriceList> builder)
    {
        builder.ToTable("price_lists");
        builder.ApplyEntityConventions();

        builder.Property(p => p.SiteId).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();

        builder.HasMany(p => p.Entries)
            .WithOne()
            .HasForeignKey(e => e.PriceListId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(p => p.Entries).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(p => p.SiteId);
    }
}
