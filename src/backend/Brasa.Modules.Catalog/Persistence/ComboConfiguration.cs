using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class ComboConfiguration : IEntityTypeConfiguration<Combo>
{
    public void Configure(EntityTypeBuilder<Combo> builder)
    {
        builder.ToTable("combos");
        builder.ApplyEntityConventions();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.MapMoney(c => c.Price, "price");

        builder.HasMany(c => c.Components)
            .WithOne()
            .HasForeignKey(cc => cc.ComboId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(c => c.Components).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
