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
/// <b>Known gap:</b> registered after <c>UseSerilogRequestLogging()</c> in
/// <c>Program.cs</c> (which itself runs early, deliberately, so a request
/// short-circuited by CORS or the rate limiter still gets a completion log
/// line — see the comments there), so unlike
/// <see cref="Brasa.Api.ClientVersioning.ClientVersionMiddleware"/> this
/// does <b>not</b> reach that one summary line, only everything logged
/// during the request itself (EF Core commands, application code). Reaching
/// the summary line too would mean moving tenant resolution earlier in the
/// pipeline, which is a real pipeline-ordering change, not a slot in this
/// middleware — tracked as a known limitation, not implied to already work.
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
