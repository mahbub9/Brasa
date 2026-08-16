using Brasa.Modules.Ordering.Domain;
using Brasa.Shared.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Modules.Ordering.Persistence;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("orders");
        builder.ApplyEntityConventions();

        builder.Property(o => o.TableId).IsRequired();
        builder.Property(o => o.TableLabel).HasMaxLength(100).IsRequired();
        builder.Property(o => o.CoverCount).IsRequired();
        builder.Property(o => o.IsTakeaway).IsRequired();
        builder.Property(o => o.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(o => o.OpenedAtUtc).IsRequired();
        builder.Property(o => o.ClosedAtUtc);
        builder.Property(o => o.DiscountKind).HasConversion<string>().HasMaxLength(20);
        builder.Property(o => o.DiscountValue).HasColumnType("numeric(10,2)");

        // Total, LinesSubtotal and OrderDiscountAmount are all derived and must
        // never be persisted separately — a stored total could drift from the
        // lines (and discount) that produce it.
        builder.Ignore(o => o.Total);
        builder.Ignore(o => o.LinesSubtotal);
        builder.Ignore(o => o.OrderDiscountAmount);

        builder.HasMany(o => o.Lines)
            .WithOne()
            .HasForeignKey(l => l.OrderId)
            .OnDelete(DeleteBehavior.Cascade);

        // Lines is exposed as IReadOnlyList<OrderLine> with no public setter, so
        // EF Core tracks the change through the private `_lines` backing field
        // rather than expecting property-based access.
        builder.Navigation(o => o.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(o => o.Status);

        // Every order-mutating endpoint used to do a blind UPDATE-by-id with
        // nothing in its WHERE clause to notice a second, concurrent writer
        // already got there first — two terminals racing to add a line, void
        // one, or close the order could silently drop each other's change
        // with no error at all (ORD-21). xmin (Postgres's built-in row
        // version system column — no migration needed, it already exists on
        // every row) turns that blind UPDATE into a compare-and-swap, the
        // exact same mechanism TableConfiguration.cs already uses for the
        // table-occupy race. See OrderEndpoints.TrySaveOrderAsync for how
        // the resulting DbUpdateConcurrencyException is caught and turned
        // into a 409, and CloseOrderAsync's own remarks for the one call
        // site that deliberately handles it differently.
        builder.Property<uint>("xmin").IsRowVersion();
    }
}
