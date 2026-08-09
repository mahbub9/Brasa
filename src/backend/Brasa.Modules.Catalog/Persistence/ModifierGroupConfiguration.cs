using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class ModifierGroupConfiguration : IEntityTypeConfiguration<ModifierGroup>
{
    public void Configure(EntityTypeBuilder<ModifierGroup> builder)
    {
        builder.ToTable("modifier_groups");
        builder.ApplyEntityConventions();

        builder.Property(g => g.MenuItemId).IsRequired();
        builder.Property(g => g.Name).HasMaxLength(200).IsRequired();
        builder.Property(g => g.IsRequired).IsRequired();
        builder.Property(g => g.MinSelect).IsRequired();
        builder.Property(g => g.MaxSelect).IsRequired();
        builder.Property(g => g.DisplayOrder).IsRequired();

        builder.HasMany(g => g.Modifiers)
            .WithOne()
            .HasForeignKey(m => m.ModifierGroupId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(g => g.Modifiers).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(g => g.MenuItemId);
    }
}
