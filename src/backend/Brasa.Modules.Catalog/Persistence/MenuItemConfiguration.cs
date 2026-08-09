using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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
        builder.Property(i => i.Description).HasMaxLength(1000);
        builder.Property(i => i.IsAlcoholic).IsRequired();
        builder.Property(i => i.IsAvailable).IsRequired();
        builder.Property(i => i.Course).HasConversion<string>().HasMaxLength(20);
        builder.Property(i => i.DeletedAtUtc);

        // Stored as a comma-joined list of enum names rather than a Postgres
        // array column — one portable pattern, matching the string
        // conversion every other enum in this codebase already uses
        // (VatRate, TableState, OrderStatus), rather than adding a
        // provider-specific array mapping just for this one property.
        builder.Property(i => i.Allergens)
            .HasConversion(
                allergens => string.Join(',', allergens),
                value => string.IsNullOrEmpty(value)
                    ? new List<Allergen>()
                    : value.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(a => Enum.Parse<Allergen>(a))
                        .ToList(),
                new ValueComparer<IReadOnlyList<Allergen>>(
                    (a, b) => (a ?? Array.Empty<Allergen>()).SequenceEqual(b ?? Array.Empty<Allergen>()),
                    a => a.Aggregate(0, (hash, allergen) => HashCode.Combine(hash, allergen)),
                    a => a.ToList()))
            .HasColumnName("allergens")
            .HasMaxLength(200)
            .IsRequired();

        builder.MapMoney(i => i.Price, "price");

        // VatRate is a single-value wrapper around a fraction; a full conversion
        // table arrives with the I1 TaxRule model (see VatRate's remarks).
        builder.Property(i => i.VatRate)
            .HasConversion(rate => rate.Fraction, fraction => new VatRate(fraction))
            .HasColumnName("vat_rate")
            .HasColumnType("numeric(4,2)")
            .IsRequired();

        builder.HasIndex(i => i.CategoryId);

        builder.HasMany(i => i.ModifierGroups)
            .WithOne()
            .HasForeignKey(g => g.MenuItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(i => i.ModifierGroups).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
