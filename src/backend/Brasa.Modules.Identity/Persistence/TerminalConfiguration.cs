using Brasa.Modules.Identity.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Identity.Persistence;

internal sealed class TerminalConfiguration : IEntityTypeConfiguration<Terminal>
{
    public void Configure(EntityTypeBuilder<Terminal> builder)
    {
        builder.ToTable("terminals");
        builder.ApplyEntityConventions();

        builder.Property(t => t.SiteId).IsRequired();
        builder.Property(t => t.Label).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.SiteId);
    }
}
