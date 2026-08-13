using Brasa.Modules.Identity.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Identity.Persistence;

internal sealed class FeatureFlagConfiguration : IEntityTypeConfiguration<FeatureFlag>
{
    public void Configure(EntityTypeBuilder<FeatureFlag> builder)
    {
        builder.ToTable("feature_flags");
        builder.ApplyEntityConventions();

        builder.Property(f => f.Key).HasMaxLength(100).IsRequired();
        builder.Property(f => f.Platform).HasMaxLength(20).IsRequired();
        builder.Property(f => f.IsEnabled).IsRequired();

        // Platform is never null (see FeatureFlag's own remarks on why), so
        // this composite index actually enforces "one row per key/platform
        // per tenant" including the AllPlatforms sentinel case — a nullable
        // column would have silently let that one case duplicate, since
        // Postgres never treats two NULLs as equal in a unique index.
        builder.HasIndex(f => new { f.TenantId, f.Key, f.Platform }).IsUnique();
    }
}
