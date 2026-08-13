using Brasa.Shared.Tenancy;
using Serilog.Context;

namespace Brasa.Api.Tenancy;

/// <summary>
/// Pushes the resolved <see cref="ITenantContext"/> onto Serilog's
/// <see cref="LogContext"/> for the rest of the request (OPS-07) — every
/// log line written downstream, not just ones an endpoint writes on
/// purpose. Every tenant-owned table already carries <c>TenantId</c> in two
/// independent places (an EF Core query filter and an RLS policy, see
/// <c>docs/architecture/multi-tenancy.md</c>); this is the same idea for
/// logs, so a production incident can be filtered to one tenant without
/// grepping every table id it touched by hand.
/// </summary>
/// <remarks>
/// Must run after tenant resolution (<see cref="DevTenantMiddleware"/>
/// today, real auth later) — <see cref="ITenantContext.TenantId"/> is
/// <see cref="Guid.Empty"/> before that.
/// <para>
/// Registered after <c>UseSerilogRequestLogging()</c> in <c>Program.cs</c>
/// (which itself runs early, deliberately, so a request short-circuited by
/// CORS or the rate limiter still gets a completion log line), so unlike
/// <see cref="Brasa.Api.ClientVersioning.ClientVersionMiddleware"/> this
/// middleware's own <see cref="LogContext.PushProperty(string, object,
/// bool)"/> scopes are already disposed by the time control returns to
/// <c>UseSerilogRequestLogging</c>'s own completion-log call — pushed
/// properties don't survive past the <c>using</c> block that pushed them.
/// The completion line still carries the same ids, just via a different
/// mechanism: <c>Program.cs</c>'s own <c>EnrichDiagnosticContext</c>
/// callback reads <see cref="ITenantContext"/> straight from DI at
/// completion time instead, which works precisely because it's a scoped
/// per-request service holding its resolved value for the request's whole
/// lifetime, not an ambient log-context stack tied to this middleware's own
/// call frame.
/// </para>
/// </remarks>
public sealed class TenantLoggingMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        using (LogContext.PushProperty("TenantId", tenantContext.TenantId))
        using (PushIfPresent("SiteId", tenantContext.SiteId))
        using (PushIfPresent("TerminalId", tenantContext.TerminalId))
        using (PushIfPresent("UserId", tenantContext.UserId))
        {
            await next(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// A no-op disposable when <paramref name="value"/> is null, so an
    /// unresolved dimension (every request today has no <c>SiteId</c>,
    /// <c>TerminalId</c> or <c>UserId</c> — there's no auth yet) is simply
    /// absent from the log event rather than appearing as a literal "null".
    /// </summary>
    private static IDisposable PushIfPresent(string name, Guid? value)
        => value is { } id ? LogContext.PushProperty(name, id) : NullScope.Instance;

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
