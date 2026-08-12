using Brasa.Modules.Floor.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Floor.Persistence;

internal sealed class RoomConfiguration : IEntityTypeConfiguration<Room>
{
    public void Configure(EntityTypeBuilder<Room> builder)
    {
        builder.ToTable("rooms");
        builder.ApplyEntityConventions();

        builder.Property(r => r.Name).HasMaxLength(200).IsRequired();
        builder.Property(r => r.DisplayOrder).IsRequired();
        builder.Property(r => r.FloorLevel).IsRequired();
    }
}
