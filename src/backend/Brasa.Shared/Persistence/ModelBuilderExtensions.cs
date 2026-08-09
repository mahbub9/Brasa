using System.Linq.Expressions;
using Brasa.Shared.Primitives;
using Brasa.Shared.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Brasa.Shared.Persistence;

/// <summary>
/// Model conventions every module's <see cref="DbContext"/> applies.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Adds <c>WHERE tenant_id = @current</c> to every entity implementing
    /// <see cref="ITenantOwned"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is a <b>convenience, not a security boundary</b>. It is bypassed by
    /// raw SQL, by Dapper, and by a single forgotten <c>IgnoreQueryFilters()</c>.
    /// The row-level security policy applied in
    /// <see cref="RowLevelSecurity.EnableFor(Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder, string, string)"/>
    /// is what actually protects the data.
    /// </para>
    /// <para>See <c>docs/architecture/multi-tenancy.md</c>.</para>
    /// </remarks>
    public static void ApplyTenantQueryFilters(this ModelBuilder modelBuilder, ITenantContextAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(modelBuilder);
        ArgumentNullException.ThrowIfNull(accessor);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(ITenantOwned).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // entity => EF.Property<Guid>(entity, "TenantId") == accessor.CurrentTenantId
            var parameter = Expression.Parameter(entityType.ClrType, "entity");
            var tenantIdProperty = Expression.Call(
                typeof(EF),
                nameof(EF.Property),
                [typeof(Guid)],
                parameter,
                Expression.Constant(nameof(ITenantOwned.TenantId)));

            var currentTenant = Expression.Property(
                Expression.Constant(accessor),
                nameof(ITenantContextAccessor.CurrentTenantId));

            Expression body = Expression.Equal(tenantIdProperty, currentTenant);

            // Soft-deleted rows stay in the table (see ISoftDeletable — historical
            // orders and fiscal documents must keep reading them) but disappear
            // from every ordinary query the same way a tenant mismatch does. A
            // query that genuinely needs deleted rows (an admin "show deleted"
            // view) uses IgnoreQueryFilters() explicitly, so that intent is
            // visible at the call site rather than silently assumed everywhere.
            if (typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType))
            {
                var deletedAtProperty = Expression.Call(
                    typeof(EF),
                    nameof(EF.Property),
                    [typeof(DateTimeOffset?)],
                    parameter,
                    Expression.Constant(nameof(ISoftDeletable.DeletedAtUtc)));

                var notDeleted = Expression.Equal(
                    deletedAtProperty,
                    Expression.Constant(null, typeof(DateTimeOffset?)));

                body = Expression.AndAlso(body, notDeleted);
            }

            modelBuilder.Entity(entityType.ClrType)
                .HasQueryFilter(Expression.Lambda(body, parameter));
        }
    }

    /// <summary>
    /// Maps a <see cref="Money"/> property to two columns — the integer minor
    /// units and the currency — under a shared prefix.
    /// </summary>
    /// <remarks>
    /// Money is never stored as a decimal or a float. Storing the raw minor units
    /// means the database cannot round, and a total read back is exactly the total
    /// written. See <c>docs/architecture/money.md</c>.
    /// </remarks>
    public static ComplexPropertyBuilder<Money> MapMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money>> property,
        string columnPrefix)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        var complex = builder.ComplexProperty(property);

        complex.Property(m => m.MinorUnits)
            .HasColumnName($"{columnPrefix}_minor_units")
            .HasColumnType("bigint")
            .IsRequired();

        complex.Property(m => m.Currency)
            .HasColumnName($"{columnPrefix}_currency")
            .HasConversion<string>()
            .HasMaxLength(3)
            .IsRequired();

        return complex;
    }

    /// <summary>
    /// Same as <see cref="MapMoney{TEntity}"/>, for a <see cref="Money"/> that
    /// may be genuinely absent (not just zero) — e.g. <c>MenuItem.TakeawayPrice</c>
    /// (CAT-06), where "no separate takeaway price set" and "free" are different
    /// things. Both columns are nullable together; there is no state where one
    /// is set and the other isn't.
    /// </summary>
    public static ComplexPropertyBuilder<Money> MapOptionalMoney<TEntity>(
        this EntityTypeBuilder<TEntity> builder,
        Expression<Func<TEntity, Money?>> property,
        string columnPrefix)
        where TEntity : class
    {
        ArgumentNullException.ThrowIfNull(builder);

        var complex = builder.ComplexProperty(property);
        complex.IsRequired(false);

        complex.Property(m => m.MinorUnits)
            .HasColumnName($"{columnPrefix}_minor_units")
            .HasColumnType("bigint");

        complex.Property(m => m.Currency)
            .HasColumnName($"{columnPrefix}_currency")
            .HasConversion<string>()
            .HasMaxLength(3);

        return complex;
    }

    /// <summary>
    /// Applies the shared column conventions for <see cref="Entity"/>: primary
    /// key, tenant index, and audit columns.
    /// </summary>
    public static void ApplyEntityConventions<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : Entity
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.HasKey(e => e.Id);

        // Ids are always generated client-side (UUIDv7, see Entity) — a
        // disconnected terminal mints its own without a round trip. Without this,
        // EF Core's default Guid-key convention is ValueGeneratedOnAdd, which
        // makes it assume any *new* entity reached only through a navigation
        // property (never explicitly Add()-ed) with a non-default key might
        // already exist in the database, and tracks it as Modified instead of
        // Added — exactly the case for a new OrderLine appended to a tracked
        // Order's collection. That silently corrupts inserts into no-op updates
        // and trips the tenant-reassignment guard in TenantAwareDbContext. This
        // has no effect on the generated column DDL — Guid columns never had a
        // database default — it only fixes EF's in-memory state detection.
        builder.Property(e => e.Id).ValueGeneratedNever();

        // Every tenant-scoped query filters on tenant_id, and the RLS policy adds
        // the same predicate, so this index carries essentially all read traffic.
        builder.HasIndex(e => e.TenantId);

        builder.Property(e => e.CreatedAtUtc).IsRequired();
    }
}
