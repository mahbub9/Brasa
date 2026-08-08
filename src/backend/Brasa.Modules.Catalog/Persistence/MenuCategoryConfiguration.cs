using Brasa.Modules.Catalog.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Catalog.Persistence;

internal sealed class MenuCategoryConfiguration : IEntityTypeConfiguration<MenuCategory>
{
    public void Configure(EntityTypeBuilder<MenuCategory> builder)
    {
        builder.ToTable("menu_categories");
        builder.ApplyEntityConventions();

        builder.Property(c => c.Name).HasMaxLength(200).IsRequired();
        builder.Property(c => c.DisplayOrder).IsRequired();
        builder.Property(c => c.IsVisible).IsRequired();
    }
}
