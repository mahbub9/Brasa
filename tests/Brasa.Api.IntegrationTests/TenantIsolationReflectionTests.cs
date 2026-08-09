using Brasa.Modules.Catalog.Persistence;
using Brasa.Modules.Floor.Persistence;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// DAT-11 — every entity mapped by every module's <c>DbContext</c> must
/// implement <see cref="ITenantOwned"/>, or be named on the explicit
/// allow-list below with a reason.
/// </summary>
/// <remarks>
/// <para>
/// This is reflection against each context's already-built EF model, not a
/// live query — building a model never opens a connection, so this runs as
/// fast as a unit test despite living in the integration-test project (the
/// only project with a reference to every module, which is what a test like
/// this genuinely needs).
/// </para>
/// <para>
/// The query filter (<c>ApplyTenantQueryFilters</c>) and the RLS policy
/// (<c>RowLevelSecurity.EnableFor</c>) both only ever get applied to entities
/// someone remembered to wire up. This test is the backstop for the entity
/// someone forgot — new module, new entity, no <see cref="ITenantOwned"/>,
/// and every query silently leaks across tenants until it's noticed in
/// production. See <c>docs/architecture/multi-tenancy.md</c>.
/// </para>
/// </remarks>
public class TenantIsolationReflectionTests
{
    // Genuinely tenant-free reference data goes here, with a reason each time
    // an entry is added. Empty today — every mapped entity in the system is
    // tenant-owned.
    private static readonly HashSet<Type> AllowListedNonTenantEntities = [];

    public static TheoryData<string, Type[]> ModuleEntityTypes()
    {
        var data = new TheoryData<string, Type[]>();

        using (var db = BuildContext<CatalogDbContext>(o => new CatalogDbContext(o, new TenantContext(), new TenantContextAccessor(), new SystemClock())))
        {
            data.Add("Catalog", [.. db.Model.GetEntityTypes().Select(t => t.ClrType)]);
        }

        using (var db = BuildContext<OrderingDbContext>(o => new OrderingDbContext(o, new TenantContext(), new TenantContextAccessor(), new SystemClock())))
        {
            data.Add("Ordering", [.. db.Model.GetEntityTypes().Select(t => t.ClrType)]);
        }

        using (var db = BuildContext<FloorDbContext>(o => new FloorDbContext(o, new TenantContext(), new TenantContextAccessor(), new SystemClock())))
        {
            data.Add("Floor", [.. db.Model.GetEntityTypes().Select(t => t.ClrType)]);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ModuleEntityTypes))]
    public void Every_mapped_entity_is_tenant_owned_or_explicitly_allow_listed(string moduleName, Type[] entityTypes)
    {
        entityTypes.ShouldNotBeEmpty($"{moduleName}'s DbContext mapped no entities — did the model fail to build?");

        var violations = entityTypes
            .Where(t => !typeof(ITenantOwned).IsAssignableFrom(t))
            .Where(t => !AllowListedNonTenantEntities.Contains(t))
            .ToArray();

        violations.ShouldBeEmpty(
            $"{moduleName} maps {string.Join(", ", violations.Select(t => t.Name))} without ITenantOwned. " +
            "Either implement ITenantOwned (inherit Entity, the usual path) or add it to " +
            $"{nameof(AllowListedNonTenantEntities)} with a reason — see this file's remarks.");
    }

    // A throwaway connection string that is never connected to — building an
    // EF model is metadata-only. Using localhost keeps this from ever
    // accidentally reaching a real host if that assumption is ever wrong.
    private const string NeverConnectedConnectionString =
        "Host=localhost;Port=5432;Database=brasa_dat11_reflection_only;Username=none;Password=none";

    private static TContext BuildContext<TContext>(Func<DbContextOptions<TContext>, TContext> factory)
        where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>()
            .UseNpgsql(NeverConnectedConnectionString)
            .Options;
        return factory(options);
    }
}
