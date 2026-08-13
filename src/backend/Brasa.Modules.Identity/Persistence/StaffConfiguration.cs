using Brasa.Modules.Identity.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Identity.Persistence;

internal sealed class StaffConfiguration : IEntityTypeConfiguration<Staff>
{
    public void Configure(EntityTypeBuilder<Staff> builder)
    {
        builder.ToTable("staff");
        builder.ApplyEntityConventions();

        builder.Property(s => s.SiteId).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();

        // Same string-not-int convention every enum-shaped column in this
        // schema already uses (Course, KitchenStation).
        builder.Property(s => s.Role).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property<string>("PinHash").HasColumnName("pin_hash").HasMaxLength(200).IsRequired();

        builder.Property(s => s.FailedPinAttempts).IsRequired();
        builder.Property(s => s.LockedUntilUtc);

        builder.HasIndex(s => s.SiteId);
    }
}
