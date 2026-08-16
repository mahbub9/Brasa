using System.Globalization;
using System.Threading.RateLimiting;
using Asp.Versioning;
using Brasa.Api;
using Brasa.Api.ClientVersioning;
using Brasa.Api.Endpoints;
using Brasa.Api.HealthChecks;
using Brasa.Api.Hubs;
using Brasa.Api.Idempotency;
using Brasa.Api.Jobs;
using Brasa.Api.RateLimiting;
using Brasa.Api.Seed;
using Brasa.Api.Tenancy;
using Brasa.Api.Testing;
using Brasa.Fiscal.Mock;
using Brasa.Modules.Catalog;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Modules.Floor;
using Brasa.Modules.Floor.Persistence;
using Brasa.Modules.Identity;
using Brasa.Modules.Identity.Persistence;
using Brasa.Modules.Ordering;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Modules.Payments;
using Brasa.Modules.Payments.Persistence;
using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.ResponseCompression;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

// OPS-08 — traces and metrics, OTLP-exported to Seq (which ingests OTLP
// natively — see docs/product/status.md). No endpoint configured (the base
// appsettings.json default) means the instrumentation still runs but the
// exporter is skipped entirely, the same "config-bound, empty by default"
// seam-ahead-of-the-trigger shape as ApiDeprecationOptions: there is no real
// OTLP collector for a production deployment yet (OPS-11), only the local
// dev Seq instance (appsettings.Development.json).
var otlpEndpoint = builder.Configuration["Otel:OtlpEndpoint"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("brasa-api"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            // Npgsql emits its own spans on an ActivitySource named "Npgsql"
            // (built into Npgsql itself since v7) — subscribing to it by name
            // is what actually turns emission on, independent of the exact
            // API surface Npgsql.OpenTelemetry's own package exposes.
            .AddSource("Npgsql");

        if (otlpEndpoint is not null)
        {
            tracing.AddOtlpExporter(otlp =>
            {
                // AddOtlpExporter posts to Endpoint exactly as given — unlike
                // the OTEL_EXPORTER_OTLP_ENDPOINT env var (read by the
                // unified UseOtlpExporter() helper this isn't using), it does
                // NOT append a per-signal path itself. Seq's OTLP ingestion
                // needs the full signal-specific path or every export 404s.
                otlp.Endpoint = new Uri($"{otlpEndpoint}/v1/traces");
                otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddRuntimeInstrumentation();

        if (otlpEndpoint is not null)
        {
            metrics.AddOtlpExporter(otlp =>
            {
                otlp.Endpoint = new Uri($"{otlpEndpoint}/v1/metrics");
                otlp.Protocol = OtlpExportProtocol.HttpProtobuf;
            });
        }
    });

// ADR 0012 — the beta's swappable-database toggle. Bound before connection
// strings are read, and guarded here (not just documented) so a
// misconfigured Production deploy can't silently serve a throwaway store:
// InMemory has no row-level security boundary and loses everything on
// restart, the same "structurally impossible in Production" shape
// AddMockFiscalProvider already uses for the same reason.
var databaseOptions = builder.Configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
if (builder.Environment.IsProduction() && databaseOptions.Provider == DatabaseProvider.InMemory)
{
    throw new InvalidOperationException(
        "Database:Provider=InMemory must never be used in Production — no row-level " +
        "security boundary, and all data is lost on restart. " +
        "See docs/architecture/decisions/0012-beta-in-memory-database.md.");
}
builder.Services.AddSingleton(databaseOptions);

// Three roles, three connection strings — see infra/initdb/01-app-role.sql
// and infra/initdb/02-system-role.sql. "Postgres" (brasa_app) is unprivileged
// and is what actually serves requests, so row-level security applies to it.
// "PostgresMigrations" (brasa) is a superuser and is used only to run
// migrations, never to answer a request. "PostgresSystem" (brasa_system,
// DAT-07) is read-only and cross-tenant, selected per-DbContext-construction
// by ModulePersistenceExtensions whenever ITenantContext.IsSystemContext is
// set — background jobs and migrations only, never a request path. Only
// needed when Database:Provider is Postgres — InMemory (ADR 0012) needs
// none of the three, so all stay empty rather than throwing in that mode.
var connectionString = string.Empty;
var migrationsConnectionString = string.Empty;
var systemConnectionString = string.Empty;
if (databaseOptions.Provider == DatabaseProvider.Postgres)
{
    connectionString = builder.Configuration.GetConnectionString("Postgres")
        ?? throw new InvalidOperationException("ConnectionStrings:Postgres is not configured.");
    migrationsConnectionString = builder.Configuration.GetConnectionString("PostgresMigrations")
        ?? throw new InvalidOperationException("ConnectionStrings:PostgresMigrations is not configured.");
    systemConnectionString = builder.Configuration.GetConnectionString("PostgresSystem")
        ?? throw new InvalidOperationException("ConnectionStrings:PostgresSystem is not configured.");
}

// ── Shared kernel ───────────────────────────────────────────────────────────
// TestableClock (QA-04) replaces the plain SystemClock registration so
// TestClockMiddleware has something to override. Singleton, not scoped —
// MockFiscalProvider is deliberately singleton (its in-memory sequential
// numbering must survive across requests), and a singleton cannot consume
// a scoped IClock (ASP.NET Core's own DI validation refuses to start the
// app if it tries — caught exactly this way). Per-request isolation comes
// from TestableClock's own internal AsyncLocal instead of DI scoping — see
// its remarks. It behaves exactly like SystemClock until something calls
// OverrideWith, which only that Production-guarded middleware ever does.
builder.Services.AddSingleton<TestableClock>();
builder.Services.AddSingleton<IClock>(sp => sp.GetRequiredService<TestableClock>());
builder.Services.AddBrasaTenancy();
builder.Services.AddMemoryCache();

// CAT-02 — local-disk menu item photo storage. See MenuItemImageStorage's
// own remarks for why this is a dev/demo-scoped placeholder, not a real
// production upload pipeline.
builder.Services.AddSingleton<MenuItemImageStorage>();

// API-07 — per-client-id version policy, keyed by the X-Brasa-Client
// header's client-id (ClientVersionMiddleware). Config-bound rather than a
// database table: there is no admin UI to edit this yet, and it changes
// about as often as a deployment does.
builder.Services.Configure<Dictionary<string, ClientRequirementEntry>>(
    builder.Configuration.GetSection("ClientRequirements"));

// API-08 — RFC 8594 Deprecation/Sunset headers for the whole /api/v1
// surface. Empty by default; set once a real /api/v2 exists and v1 is
// scheduled for removal (docs/architecture/api-contract.md §3).
builder.Services.Configure<ApiDeprecationOptions>(builder.Configuration.GetSection("Api:Deprecation"));

// API-12 — per-(tenant, X-Brasa-Client client id) rate limiting on
// /api/**. See ApiRateLimiting.ResolvePartitionKey for the partitioning
// and RateLimitingOptions for the defaults.
builder.Services.Configure<RateLimitingOptions>(builder.Configuration.GetSection("RateLimiting"));
builder.Services.AddRateLimiter(limiterOptions =>
{
    limiterOptions.OnRejected = async (context, cancellationToken) =>
    {
        var retryAfterSeconds = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
            ? (int)retryAfter.TotalSeconds
            : context.HttpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value.WindowSeconds;
        context.HttpContext.Response.Headers.RetryAfter = retryAfterSeconds.ToString(CultureInfo.InvariantCulture);

        await Error.RateLimited(
                "request.rate_limited",
                "Too many requests — retry after the window resets.")
            .ToProblem()
            .ExecuteAsync(context.HttpContext)
            .ConfigureAwait(false);
    };

    limiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        var partitionKey = ApiRateLimiting.ResolvePartitionKey(httpContext, tenantContext);

        if (partitionKey == ApiRateLimiting.UnmeteredPartitionKey)
        {
            return RateLimitPartition.GetNoLimiter(partitionKey);
        }

        var rateLimiting = httpContext.RequestServices.GetRequiredService<IOptions<RateLimitingOptions>>().Value;
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = rateLimiting.PermitLimit,
            Window = TimeSpan.FromSeconds(rateLimiting.WindowSeconds),
            QueueLimit = 0,
        });
    });
});

// ── Modules ──────────────────────────────────────────────────────────────────
builder.Services.AddCatalogModule(databaseOptions, connectionString, systemConnectionString);
builder.Services.AddOrderingModule(databaseOptions, connectionString, systemConnectionString);
builder.Services.AddFloorModule(databaseOptions, connectionString, systemConnectionString);
builder.Services.AddIdentityModule(databaseOptions, connectionString, systemConnectionString);
builder.Services.AddPaymentsModule(databaseOptions, connectionString, systemConnectionString);

// Fiscal.Portugal (the real, AT-certifiable engine) is I7 work — see
// docs/architecture/decisions/0002-own-fiscal-engine.md. Until it exists, there
// is no code path that can legally issue a real document, so AddMockFiscalProvider's
// own Production guard is what keeps a Production boot from ever succeeding
// today. That is correct: it should fail closed, not silently serve mock
// documents to a real restaurant.
builder.Services.AddMockFiscalProvider(builder.Environment);

// ── Background jobs (OPS-10) ─────────────────────────────────────────────────
// Uses the migrations (superuser) connection, the same reasoning as
// MigrateAsync below — Hangfire's own storage schema needs DDL rights the
// unprivileged runtime role doesn't have. DatabaseBackupJob (Jobs/) wraps
// the already-verified backup/restore-drill scripts; this is what closes
// OPS-12's own "nothing schedules it yet" gap. InMemory (ADR 0012) has
// nothing for DatabaseBackupJob to back up — see the recurring-job guard
// below — so Hangfire's own storage just needs to not require Postgres.
builder.Services.AddHangfire(config =>
{
    config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings();

    if (databaseOptions.Provider == DatabaseProvider.InMemory)
    {
        config.UseInMemoryStorage();
    }
    else
    {
        config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(migrationsConnectionString));
    }
});
builder.Services.AddHangfireServer();

// ── API platform ─────────────────────────────────────────────────────────────
// RFC 9457 responses for every failure, so clients get one error shape.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

// API-11, docs/architecture/api-contract.md §9: "Compression on all
// responses" — assume a phone on cellular in a basement dining room. Safe to
// enable over HTTPS here: BREACH relies on a compressed response mixing a
// secret with attacker-influenced content (classically a CSRF token
// reflected next to a query parameter in server-rendered HTML), and this API
// has neither — bearer-token auth with no cookies (ADR 0008), and every
// response is generated server-side from the database, never echoing
// caller-supplied content back into the same body as a secret.
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
    options.MimeTypes = ResponseCompressionDefaults.MimeTypes.Append("application/problem+json");
});

// "ready" is tagged separately from the untagged liveness checks (there are
// none) so /health stays a pure "is the process up" probe and /health/ready
// is the one that actually depends on the database — see the mapping below.
// InMemory (ADR 0012) has nothing to probe, so InMemoryDatabaseHealthCheck
// swaps in and always reports healthy, keeping /health/ready's shape
// (one check, tagged "ready") the same either way.
builder.Services.AddHealthChecks()
    .AddCheck(
        "database",
        databaseOptions.Provider == DatabaseProvider.InMemory
            ? new InMemoryDatabaseHealthCheck()
            : new DatabaseHealthCheck(connectionString),
        tags: ["ready"]);

builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// API-16 — this codebase's first realtime channel. See FloorHub's own
// remarks: broadcast-only, payload-less, no server-invokable methods, so
// no hub-specific service configuration beyond the default is needed.
builder.Services.AddSignalR();

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
            // ETag and X-Next-Cursor are not CORS "simple" response headers
            // browsers expose to JS by default (API-10, API-09) — without
            // this, a web client could never read ETag to send back as
            // If-None-Match, or read X-Next-Cursor to fetch the next page.
            policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("ETag", "X-Next-Cursor");
        }
    });
});

var app = builder.Build();

// ── Startup: migrate and seed (never in Production — see the guards above) ──
if (!app.Environment.IsProduction())
{
    // InMemory (ADR 0012, SQLite :memory:) has no migrations — EnsureCreatedAsync
    // builds each module's schema straight from the model instead.
    if (databaseOptions.Provider == DatabaseProvider.Postgres)
    {
        await MigrateAsync(migrationsConnectionString, CancellationToken.None).ConfigureAwait(false);
    }
    else
    {
        await EnsureCreatedAsync(app.Services, CancellationToken.None).ConfigureAwait(false);
    }

    // Database:SeedOnStartup (default true, matching today's behavior) is
    // flipped off for a beta pilot once real menu/floor/staff data has been
    // entered — InMemory is wiped on every restart, so leaving this on
    // forever would silently reseed demo placeholders over live data.
    if (databaseOptions.SeedOnStartup)
    {
        await DevCatalogSeeder.SeedAsync(app.Services, app.Environment, CancellationToken.None).ConfigureAwait(false);
        await DevFloorSeeder.SeedAsync(app.Services, app.Environment, CancellationToken.None).ConfigureAwait(false);
        await DevIdentitySeeder.SeedAsync(app.Services, app.Environment, CancellationToken.None).ConfigureAwait(false);
    }
}

// Must be one of the first middlewares in the pipeline — anything that
// writes to the response before this runs bypasses compression entirely.
app.UseResponseCompression();

// Runs before request logging so a parsed X-Brasa-Client header (API-06)
// enriches the request-completion log line itself, not just lines written
// after it.
app.UseMiddleware<ClientVersionMiddleware>();

// API-08 — a no-op today (see ApiDeprecationOptions), cheap enough to run
// unconditionally rather than gate behind whether anything is configured.
app.UseMiddleware<ApiDeprecationMiddleware>();

// OPS-07 — EnrichDiagnosticContext runs at request-completion time, in the
// outer middleware's own stack frame, reading straight from DI rather than
// depending on Serilog.Context.LogContext's push/pop timing. That's what
// makes this reach the completion line itself even though tenant
// resolution (DevTenantMiddleware) and TenantLoggingMiddleware both run
// later in the pipeline, inside this middleware's own next() call: by the
// time TenantLoggingMiddleware's LogContext.PushProperty scopes have been
// disposed and control returns here, ITenantContext itself (a scoped
// per-request service, not an ambient log-context stack) still holds
// whatever it resolved. No pipeline reordering needed after all.
app.UseSerilogRequestLogging(options =>
{
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        var tenantContext = httpContext.RequestServices.GetRequiredService<ITenantContext>();
        diagnosticContext.Set("TenantId", tenantContext.TenantId);
        if (tenantContext.SiteId is { } siteId)
        {
            diagnosticContext.Set("SiteId", siteId);
        }

        if (tenantContext.TerminalId is { } terminalId)
        {
            diagnosticContext.Set("TerminalId", terminalId);
        }

        if (tenantContext.UserId is { } userId)
        {
            diagnosticContext.Set("UserId", userId);
        }
    };
});
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

// CAT-02 — serves uploaded menu item photos back at /uploads/menu-items/*.
// No auth gate (this API has none yet at all) and no tenant scoping (a
// known, named gap — see MenuItemImageStorage's own remarks); a real
// multi-tenant deployment needs more than one shared local folder.
var imageStorage = app.Services.GetRequiredService<MenuItemImageStorage>();
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new PhysicalFileProvider(imageStorage.RootPath),
    RequestPath = "/uploads/menu-items",
});

// OPS-10 — Hangfire's dashboard has no authorization filter configured:
// this app has no auth story at all yet (IDN-03…08, I3), so there is
// nothing to gate it behind today. Mapped only outside Production, the
// same "structurally impossible in Production" shape DevTenantMiddleware
// and TestClockMiddleware already use, and doubly true here since
// DatabaseBackupJob's own scripts only work against the local dev Docker
// container anyway.
if (!app.Environment.IsProduction())
{
    app.UseHangfireDashboard("/hangfire");
}

app.UseRouting();
app.UseCors(WebClientsCorsPolicy);

// Tenant resolution must run before anything that reads ITenantContext —
// the idempotency cache key, the rate limiter's partition key, and every
// module's RLS session variable all depend on it.
app.UseMiddleware<DevTenantMiddleware>();

// QA-04 — must run before any handler resolves IClock, which every one of
// them does indirectly the moment it needs UtcNow. Order relative to
// DevTenantMiddleware doesn't matter; placed right after it since both are
// the same "structurally impossible in Production" shape.
app.UseMiddleware<TestClockMiddleware>();

// OPS-07 — every log line for the rest of the request carries TenantId
// (and Site/Terminal/User ids once auth populates them). The completion
// line itself gets the same ids a different way — see the
// EnrichDiagnosticContext callback on UseSerilogRequestLogging above.
app.UseMiddleware<TenantLoggingMiddleware>();

// API-12 — before IdempotencyMiddleware on purpose: a request this rejects
// shouldn't also consume an idempotency cache slot.
app.UseRateLimiter();

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

// API-18 — deep-link verification documents (api-contract.md §12), served
// so universal/app links work with no app-specific backend logic once a
// native app exists (MOB-01+, not started). Honestly empty today — no
// bundle id, Team id or package name exists to put in either file, and
// inventing placeholder ones would make a real security-verification
// document lie. An empty "apps"/"details" array or an empty top-level
// array is the documented, structurally valid way both platforms spell
// "nothing is linked yet" — not a stub that happens to 404.
app.MapGet("/.well-known/apple-app-site-association", () => Results.Json(new { applinks = new { apps = Array.Empty<string>(), details = Array.Empty<object>() } }));
app.MapGet("/.well-known/assetlinks.json", () => Results.Json(Array.Empty<object>()));

var apiVersionSet = app.NewApiVersionSet()
    .HasApiVersion(new ApiVersion(1, 0))
    .ReportApiVersions()
    .Build();

var v1 = app.MapGroup("/api/v1")
    .WithApiVersionSet(apiVersionSet)
    .MapToApiVersion(1, 0);

v1.MapGet("/ping", (IClock clock) => Results.Ok(new
{
    service = "brasa-api",
    utc = clock.UtcNow.ToString("O", CultureInfo.InvariantCulture),
}))
.WithName("Ping")
.WithSummary("Cheap reachability check used by the POS to decide whether the cloud is available.");

v1.MapCatalogEndpoints();
v1.MapFloorEndpoints();
v1.MapIdentityEndpoints();
v1.MapPriceListEndpoints();
v1.MapComboEndpoints();
v1.MapTaxRuleEndpoints();
v1.MapOrderEndpoints();
v1.MapPaymentEndpoints();
v1.MapClientEndpoints();

// Not under /api/v1 -- a hub is a connection, not a versioned resource.
// API-16/17: this codebase's first realtime channel, broadcast-only,
// payload-less (see FloorHub's own remarks for why).
app.MapHub<FloorHub>("/hubs/floor");

// OPS-10/OPS-12 — a routine backup daily, the full (backup + restore +
// row-count) drill weekly, same split docs/development/backup-and-restore.md
// itself already suggested before either job existed: the drill is the
// expensive half, worth running periodically rather than on every backup.
// Times are UTC (Hangfire's Cron default) -- an unattended backup job has
// no reason to care about Europe/Lisbon vs Azores, unlike fiscal daily
// close. Never in Production -- see DatabaseBackupJob's own remarks.
// Postgres-only: DatabaseBackupJob wraps real pg_dump/pg_restore, meaningless
// against InMemory (ADR 0012).
if (!app.Environment.IsProduction() && databaseOptions.Provider == DatabaseProvider.Postgres)
{
    RecurringJob.AddOrUpdate<DatabaseBackupJob>(
        "nightly-database-backup", job => job.RunBackupAsync(CancellationToken.None), Cron.Daily(3));
    RecurringJob.AddOrUpdate<DatabaseBackupJob>(
        "weekly-restore-drill", job => job.RunRestoreDrillAsync(CancellationToken.None), Cron.Weekly(DayOfWeek.Sunday, 4));
}

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

    var identityOptions = new DbContextOptionsBuilder<IdentityDbContext>()
        .UseNpgsql(migrationsConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "identity"))
        .Options;
    await using (var identityDb = new IdentityDbContext(identityOptions, tenantContext, tenantContextAccessor, clock))
    {
        await identityDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }

    var paymentsOptions = new DbContextOptionsBuilder<PaymentsDbContext>()
        .UseNpgsql(migrationsConnectionString, npgsql => npgsql.MigrationsHistoryTable("__ef_migrations_history", "payments"))
        .Options;
    await using (var paymentsDb = new PaymentsDbContext(paymentsOptions, tenantContext, tenantContextAccessor, clock))
    {
        await paymentsDb.Database.MigrateAsync(cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Builds every module's schema straight from the EF model — the InMemory
/// (ADR 0012, SQLite <c>:memory:</c>) equivalent of <see cref="MigrateAsync"/>.
/// </summary>
/// <remarks>
/// Unlike <see cref="MigrateAsync"/>, this resolves the already
/// DI-registered contexts rather than building throwaway ones: SQLite
/// in-memory has no elevated-vs-runtime role split (ADR 0010 is a
/// PostgreSQL-only concern), so there is nothing a hand-built context would
/// need that the registered one doesn't already have.
/// </remarks>
static async Task EnsureCreatedAsync(IServiceProvider services, CancellationToken cancellationToken)
{
    await using var scope = services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CatalogDbContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    await scope.ServiceProvider.GetRequiredService<OrderingDbContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    await scope.ServiceProvider.GetRequiredService<FloorDbContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    await scope.ServiceProvider.GetRequiredService<IdentityDbContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
    await scope.ServiceProvider.GetRequiredService<PaymentsDbContext>().Database.EnsureCreatedAsync(cancellationToken).ConfigureAwait(false);
}

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the integration test
/// project can boot the real API against a Testcontainers PostgreSQL instance.
/// </summary>
public partial class Program;
