using System.Globalization;
using Asp.Versioning;
using Brasa.Api.Endpoints;
using Brasa.Api.HealthChecks;
using Brasa.Api.Idempotency;
using Brasa.Api.Seed;
using Brasa.Api.Tenancy;
using Brasa.Fiscal.Mock;
using Brasa.Modules.Catalog;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Modules.Floor;
using Brasa.Modules.Floor.Persistence;
using Brasa.Modules.Ordering;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Shared.Persistence;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Serilog;

// ─────────────────────────────────────────────────────────────────────────────
//  Brasa cloud API.
//
//  Responsibilities: tenant configuration, menu master data, reporting, SAF-T
//  submission to AT, and the back-office. It is NOT on the critical path for
//  taking an order — that runs against the Site Agent over the restaurant LAN so
//  service survives an internet outage. See docs/architecture/README.md.
//
//  I0 status (docs/product/roadmap.md): the walking skeleton. No auth — every
//  request is attributed to a single hardcoded tenant by DevTenantMiddleware,
//  guarded so it can never reach Production. See docs/product/status.md.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// Two roles, two connection strings — see infra/initdb/01-app-role.sql.
// "Postgres" (brasa_app) is unprivileged and is what actually serves requests,
// so row-level security applies to it. "PostgresMigrations" (brasa) is a
// superuser and is used only to run migrations, never to answer a request.
var connectionString = builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
var migrationsConnectionString = builder.Configuration.GetConnectionString("PostgresMigrations")
    ?? throw new InvalidOperationException("ConnectionStrings:PostgresMigrations is not configured.");

// ── Shared kernel ───────────────────────────────────────────────────────────
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddBrasaTenancy();
builder.Services.AddMemoryCache();

// ── Modules ──────────────────────────────────────────────────────────────────
builder.Services.AddCatalogModule(connectionString);
builder.Services.AddOrderingModule(connectionString);
builder.Services.AddFloorModule(connectionString);

// Fiscal.Portugal (the real, AT-certifiable engine) is I7 work — see
// docs/architecture/decisions/0002-own-fiscal-engine.md. Until it exists, there
// is no code path that can legally issue a real document, so AddMockFiscalProvider's
// own Production guard is what keeps a Production boot from ever succeeding
// today. That is correct: it should fail closed, not silently serve mock
// documents to a real restaurant.
builder.Services.AddMockFiscalProvider(builder.Environment);

// ── API platform ─────────────────────────────────────────────────────────────
// RFC 9457 responses for every failure, so clients get one error shape.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// "ready" is tagged separately from the untagged liveness checks (there are
// none) so /health stays a pure "is the process up" probe and /health/ready
// is the one that actually depends on PostgreSQL — see the mapping below.
builder.Services.AddHealthChecks()
    .AddCheck("postgres", new DatabaseHealthCheck(connectionString), tags: ["ready"]);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Web clients are separate origins from day one (Vite dev server today, real
// domains for pos/kds/admin/order later) — see docs/architecture/api-contract.md.
// Origins come from configuration, not a hardcoded localhost port, because each
// client app dev-serves on its own port.
const string WebClientsCorsPolicy = "WebClients";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebClientsCorsPolicy, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            // ETag is not one of the CORS "simple" response headers browsers
            // expose to JS by default (API-10) — without this, a web client
            // could never read it to send back as If-None-Match.
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("ETag");
        }
    });
});

var app = builder.Build();

// ── Startup: migrate and seed (never in Production — see the guards above) ──
if (!app.Environment.IsProduction())
{
    await MigrateAsync(migrationsConnectionString, CancellationToken.None).ConfigureAwait(false);
    await DevCatalogSeeder.SeedAsync(app.Services, app.Environment, CancellationToken.None).ConfigureAwait(false);
    await DevFloorSeeder.SeedAsync(app.Services, app.Environment, CancellationToken.None).ConfigureAwait(false);
}

app.UseSerilogRequestLogging();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}
else
{
    app.UseHttpsRedirection();
}

app.UseRouting();
app.UseCors(WebClientsCorsPolicy);

// Tenant resolution must run before anything that reads ITenantContext —
// the idempotency cache key and every module's RLS session variable both
// depend on it.
app.UseMiddleware<DevTenantMiddleware>();
app.UseMiddleware<IdempotencyMiddleware>();

// Liveness: is the process itself able to respond, regardless of PostgreSQL.
// Predicate excludes every registered check (all of them are tagged "ready"),
// so this always reports Healthy as long as the process can serve the
// request at all — the point of a liveness probe.
app.MapHealthChecks("/health", new HealthCheckOptions { Predicate = _ => false });

// Readiness: can this instance actually serve a request right now? OPS-09.
// A load balancer or orchestrator should stop routing traffic here — but
// not restart the process — when this fails; PostgreSQL being briefly
// unreachable is not a reason to kill and reschedule the API.
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready"),
});

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

var v1 = app.MapGroup("/api/v1")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(1, 0);

v1.MapGet("/ping", () => Results.Ok(new
{
    service = "brasa-api",
    utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
}))
.WithName("Ping")
.WithSummary("Cheap reachability check used by the POS to decide whether the cloud is available.");

v1.MapCatalogEndpoints();
v1.MapFloorEndpoints();
v1.MapOrderEndpoints();

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Runs every module's migrations against the elevated migration role.
/// </summary>
/// <remarks>
/// Builds throwaway <see cref="DbContext"/> instances directly rather than
/// resolving the DI-registered ones, because those are wired to the
/// unprivileged runtime connection string and cannot run DDL or alter RLS
/// policies. <see cref="TenantContext"/> and <see cref="TenantContextAccessor"/>
/// are only ever captured in expression trees during model building here, never
/// evaluated, so unresolved instances are safe.
/// </remarks>
static async Task MigrateAsync(string migrationsConnectionString, CancellationToken cancellationToken)
{
    var clock = new SystemClock();
    var tenantContext = new TenantContext();
    var tenantContextAccessor = new TenantContextAccessor();

    var catalogOptions = new DbContextOptionsBuilder<CatalogDbContext>()
        .UseNpgsql(migrationsConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "catalog"))
        .Options;
    await using (var catalogDb = new CatalogDbContext(catalogOptions, tenantContext, tenantContextAccessor, clock))
    {
        await catalogDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    var orderingOptions = new DbContextOptionsBuilder<OrderingDbContext>()
        .UseNpgsql(migrationsConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "ordering"))
        .Options;
    await using (var orderingDb = new OrderingDbContext(orderingOptions, tenantContext, tenantContextAccessor, clock))
    {
        await orderingDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    var floorOptions = new DbContextOptionsBuilder<FloorDbContext>()
        .UseNpgsql(migrationsConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "floor"))
        .Options;
    await using (var floorDb = new FloorDbContext(floorOptions, tenantContext, tenantContextAccessor, clock))
    {
        await floorDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the integration test
/// project can boot the real API against a Testcontainers PostgreSQL instance.
/// </summary>
public partial class Program;
