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
    }
}
