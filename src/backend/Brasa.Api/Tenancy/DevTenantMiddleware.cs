using Brasa.Shared.Tenancy;

namespace Brasa.Api.Tenancy;

/// <summary>
/// Resolves every request to the single hardcoded <see cref="DevTenant"/>.
/// </summary>
/// <remarks>
/// This is the entire "authentication" story until IDN-03…08 (OAuth 2.1 + PKCE,
/// terminal pairing, staff PIN — see <c>docs/architecture/decisions/0008-token-auth-no-cookies.md</c>)
/// land in I3. It exists so the walking skeleton exercises real tenancy and RLS
/// end to end without waiting on auth.
/// </remarks>
public sealed class DevTenantMiddleware(RequestDelegate next)
{
    /// <summary>Invokes the middleware.</summary>
    /// <exception cref="InvalidOperationException">The host environment reports Production.</exception>
    public async Task InvokeAsync(HttpContext context, TenantContext tenantContext, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(environment);

        // A hardcoded tenant reaching Production would silently attribute every
        // customer's data to one fixed id. This must be structurally impossible,
        // not merely undocumented — mirrors the MockFiscalProvider guard.
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "DevTenantMiddleware must never run in Production. " +
                "Real tenant resolution (IDN-03..08) must be wired before going live.");
        }

        tenantContext.Resolve(DevTenant.Id);
        await next(context).ConfigureAwait(false);
    }
}
