using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class TaxRuleConfiguration : IEntityTypeConfiguration<TaxRule>
{
    public void Configure(EntityTypeBuilder<TaxRule> builder)
    {
        builder.ToTable("tax_rules");
        builder.ApplyEntityConventions();

        builder.Property(r => r.IsAlcoholic).IsRequired();
        builder.Property(r => r.IsTakeaway).IsRequired();

        // Same string-not-int convention every enum-shaped column in this
        // schema already uses (Course, KitchenStation) — readable directly
        // in a database console without a lookup table.
        builder.Property(r => r.Region).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(r => r.Rate)
            .HasConversion(rate => rate.Fraction, fraction => new VatRate(fraction))
            .HasColumnName("rate")
            .HasColumnType("numeric(4,2)")
            .IsRequired();

        builder.Property(r => r.EffectiveFromUtc).IsRequired();
        builder.Property(r => r.EffectiveToUtc);

        builder.HasIndex(r => new { r.IsAlcoholic, r.IsTakeaway, r.Region });
    }
}
