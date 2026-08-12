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
        builder.Property(t => t.GroupId);

        builder.HasIndex(t => t.RoomId);
        builder.HasIndex(t => t.State);
        builder.HasIndex(t => t.GroupId);

        // Table.Occupy() is a check-then-act on in-memory state with no
        // database-level guard otherwise: two concurrent "open this table"
        // requests can both read Free, both transition in memory, and both
        // successfully UPDATE the same row — the second writer's save is a
        // blind UPDATE-by-id with nothing in its WHERE clause to notice the
        // first writer already got there. xmin (Postgres's built-in row
        // version system column — no migration needed, it already exists on
        // every row) turns that blind UPDATE into a compare-and-swap: EF
        // adds "AND xmin = @original_xmin" to the WHERE clause, so the loser
        // updates zero rows and throws DbUpdateConcurrencyException instead
        // of silently overwriting the winner. See OrderEndpoints.cs for how
        // that's caught and turned into the same 409 the in-memory check
        // already returns for the non-concurrent case.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
