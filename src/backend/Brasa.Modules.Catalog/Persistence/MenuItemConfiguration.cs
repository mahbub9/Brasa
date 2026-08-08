using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("menu_items");
        builder.ApplyEntityConventions();

        builder.Property(i => i.CategoryId).IsRequired();
        builder.Property(i => i.Name).HasMaxLength(200).IsRequired();
        builder.Property(i => i.IsAlcoholic).IsRequired();
        builder.Property(i => i.IsAvailable).IsRequired();

        builder.MapMoney(i => i.Price, "price");

        // VatRate is a single-value wrapper around a fraction; a full conversion
        // table arrives with the I1 TaxRule model (see VatRate's remarks).
        builder.Property(i => i.VatRate)
            .HasConversion(rate => rate.Fraction, fraction => new VatRate(fraction))
            .HasColumnName("vat_rate")
            .HasColumnType("numeric(4,2)")
            .IsRequired();

        builder.HasIndex(i => i.CategoryId);
    }
}
