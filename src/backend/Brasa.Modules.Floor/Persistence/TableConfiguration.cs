using Brasa.Modules.Floor.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Floor.Persistence;

internal sealed class TableConfiguration : IEntityTypeConfiguration<Table>
{
    public void Configure(EntityTypeBuilder<Table> builder)
    {
        builder.ToTable("tables");
        builder.ApplyEntityConventions();

        builder.Property(t => t.RoomId).IsRequired();
        builder.Property(t => t.Label).HasMaxLength(100).IsRequired();
        builder.Property(t => t.Seats).IsRequired();
        builder.Property(t => t.PositionX).IsRequired();
        builder.Property(t => t.PositionY).IsRequired();
        builder.Property(t => t.Shape).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(t => t.State).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.HasIndex(t => t.RoomId);
        builder.HasIndex(t => t.State);
    }
}
