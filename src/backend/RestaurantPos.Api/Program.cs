using System.Globalization;
using Serilog;

// ─────────────────────────────────────────────────────────────────────────────
//  RestaurantPos cloud API.
//
//  Responsibilities: tenant configuration, menu master data, reporting, SAF-T
//  submission to AT, and the back-office. It is NOT on the critical path for
//  taking an order — that runs against the Site Agent over the restaurant LAN so
//  service survives an internet outage. See docs/architecture/README.md.
// ─────────────────────────────────────────────────────────────────────────────

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext());

// RFC 9457 responses for every failure, so clients get one error shape.
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

builder.Services.AddHealthChecks();

var app = builder.Build();

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

// Liveness only. A readiness probe that also checks PostgreSQL arrives with the
// persistence layer.
app.MapHealthChecks("/health");

app.MapGet("/api/v1/ping", () => Results.Ok(new
{
    service = "restaurantpos-api",
    utc = DateTimeOffset.UtcNow.ToString("O", CultureInfo.InvariantCulture),
}))
.WithName("Ping")
.WithSummary("Cheap reachability check used by the POS to decide whether the cloud is available.");

await app.RunAsync().ConfigureAwait(false);

/// <summary>
/// Exposed so <c>WebApplicationFactory&lt;Program&gt;</c> in the integration test
/// project can boot the real API against a Testcontainers PostgreSQL instance.
/// </summary>
public partial class Program;
