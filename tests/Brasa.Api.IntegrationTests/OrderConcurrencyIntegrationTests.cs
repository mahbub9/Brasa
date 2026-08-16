using Brasa.Modules.Ordering.Domain;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Brasa.Api.IntegrationTests;

/// <summary>
/// ORD-21 — proves <see cref="Order"/>'s own <c>xmin</c> concurrency token
/// (<c>OrderConfiguration.cs</c>) actually protects against a lost update,
/// against a real, disposable PostgreSQL container — the same "don't just
/// assert it, run it" discipline <see cref="TenantIsolationIntegrationTests"/>
/// already uses for RLS.
/// </summary>
/// <remarks>
/// Deterministic by construction — two independent <see cref="OrderingDbContext"/>
/// instances, standing in for two different terminals, both load the same
/// row before either writes — rather than racing two real HTTP requests and
/// hoping they land close enough together. A real client-facing proof of the
/// same mechanism, through the actual API and its 409 response, lives in
/// <c>src/web/e2e/tests/order-concurrency.spec.ts</c>; this test exists
/// because that one can only ever be probabilistic about which request wins,
/// while this one can prove the underlying compare-and-swap deterministically
/// every single run.
/// </remarks>
[Collection("Migrations env var")]
public sealed class OrderConcurrencyIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("brasa")
        .WithUsername("brasa")
        .WithPassword("devonly")
        .Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        // The real migrations carry RLS GRANTs to both brasa_app (ADR 0010)
        // and brasa_system (DAT-07) now — see TestRoles's own remarks for
        // why this is a shared helper, not a copy hand-rolled per test class.
        await TestRoles.EnsureAppAndSystemRolesExistAsync(_postgres.GetConnectionString());

        // Built via OrderingDbContextFactory — the same design-time factory
        // `dotnet ef` itself uses — for the same reason
        // TenantIsolationIntegrationTests routes through CatalogDbContextFactory:
        // a hand-rolled DbContextOptionsBuilder here would be a second
        // configuration that can silently drift out of sync with the real one.
        Environment.SetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION", _postgres.GetConnectionString());
        try
        {
            await using var db = new OrderingDbContextFactory().CreateDbContext([]);
            await db.Database.MigrateAsync();
        }
        finally
        {
            Environment.SetEnvironmentVariable("BRASA_MIGRATIONS_CONNECTION", null);
        }
    }

    public async Task DisposeAsync() => await _postgres.DisposeAsync();

    [Fact]
    public async Task Two_terminals_reading_then_both_writing_the_same_order_only_let_one_win()
    {
        var tenantId = Guid.CreateVersion7();
        var orderId = await SeedOpenOrderAsync(tenantId);

        // Two independent contexts, standing in for two different terminals,
        // both load the SAME row before either one writes — exactly the
        // "read, read, write, write" interleaving that, before ORD-21, let
        // the second writer silently clobber the first with no error at all
        // (Order carried no concurrency token, so this was a blind
        // UPDATE-by-id every time).
        await using var terminalA = CreateContext(tenantId);
        await using var terminalB = CreateContext(tenantId);

        var orderAsSeenByA = await terminalA.Orders.SingleAsync(o => o.Id == orderId);
        var orderAsSeenByB = await terminalB.Orders.SingleAsync(o => o.Id == orderId);

        // Both terminals genuinely have this order loaded before either
        // writes — the race is real, not simulated by skipping a step.
        orderAsSeenByA.TableLabel.ShouldBe("Mesa 1");
        orderAsSeenByB.TableLabel.ShouldBe("Mesa 1");

        orderAsSeenByA.TransferToTable(Guid.CreateVersion7(), "Mesa 2").IsSuccess.ShouldBeTrue();
        orderAsSeenByB.TransferToTable(Guid.CreateVersion7(), "Mesa 3").IsSuccess.ShouldBeTrue();

        // Terminal A wins — its write lands on the xmin both terminals
        // actually read, so it succeeds normally.
        await terminalA.SaveChangesAsync();

        // Terminal B's own write now targets a row whose xmin has already
        // moved on. Before ORD-21 this would have silently succeeded too,
        // overwriting A's transfer with B's and leaving no trace either lost
        // — the exact class of bug this token exists to make impossible.
        await Should.ThrowAsync<DbUpdateConcurrencyException>(() => terminalB.SaveChangesAsync());

        // The database reflects only the winner's change — not a merge of
        // both, not silently the loser's.
        await using var verifyContext = CreateContext(tenantId);
        var persisted = await verifyContext.Orders.SingleAsync(o => o.Id == orderId);
        persisted.TableLabel.ShouldBe("Mesa 2");
    }

    private OrderingDbContext CreateContext(Guid tenantId)
    {
        var tenantContext = new TenantContext();
        tenantContext.Resolve(tenantId);

        return new OrderingDbContext(
            new DbContextOptionsBuilder<OrderingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options,
            tenantContext,
            new TenantContextAccessor(),
            new SystemClock());
    }

    private async Task<Guid> SeedOpenOrderAsync(Guid tenantId)
    {
        var order = Order.Open(Guid.CreateVersion7(), "Mesa 1", coverCount: 2, DateTimeOffset.UtcNow);

        await using var seedDb = CreateContext(tenantId);
        seedDb.Orders.Add(order);
        await seedDb.SaveChangesAsync();

        return order.Id;
    }
}
