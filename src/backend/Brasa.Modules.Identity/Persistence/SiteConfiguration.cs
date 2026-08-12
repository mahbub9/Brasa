using Brasa.Modules.Identity.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Identity.Persistence;

internal sealed class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");
        builder.ApplyEntityConventions();

        builder.Property(s => s.OrganizationId).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Region).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(s => s.OrganizationId);
    }
}
