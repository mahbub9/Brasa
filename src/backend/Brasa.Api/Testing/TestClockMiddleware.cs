using System.Globalization;
using Brasa.Shared.Time;

namespace Brasa.Api.Testing;

/// <summary>
/// Lets a request fix <see cref="TestableClock"/>'s <c>UtcNow</c> for its own
/// duration via an <c>X-Brasa-Test-Clock</c> header (ISO 8601, e.g.
/// <c>2026-08-13T12:00:00Z</c>) — QA-04. Built so the E2E suite can assert a
/// scheduled price change (CAT-16) or a tax rule's effective-date boundary
/// (CAT-07/08) is *not yet* active and then, in the very next request, *is*
/// active, without a real multi-second wait — see
/// <c>docs/development/e2e-testing.md</c>.
/// </summary>
/// <remarks>
/// A missing or unparseable header is simply ignored — this is a testing
/// lever, not public API surface, so there is nothing to reject with an
/// error response. Mirrors <c>DevTenantMiddleware</c>'s guard exactly:
/// registered unconditionally in the pipeline, but throws on the very first
/// request if the host environment reports Production, so a misconfigured
/// deployment fails loudly at start of traffic rather than silently letting
/// a client dictate the server's own fiscal clock.
/// </remarks>
public sealed class TestClockMiddleware(RequestDelegate next)
{
    private const string HeaderName = "X-Brasa-Test-Clock";

    /// <summary>Invokes the middleware.</summary>
    /// <exception cref="InvalidOperationException">The host environment reports Production.</exception>
    public async Task InvokeAsync(HttpContext context, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(environment);

        // A client-supplied clock reaching Production would let anyone
        // forge the instant a fiscal document is issued at. This must be
        // structurally impossible, not merely undocumented — the same
        // "throw on the very first request" guard DevTenantMiddleware uses.
        if (environment.IsProduction())
        {
            throw new InvalidOperationException(
                "TestClockMiddleware must never run in Production. " +
                "The clock must always be the real system clock outside dev/test.");
        }

        if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue)
            && DateTimeOffset.TryParse(headerValue.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var instant))
        {
            TestableClock.OverrideWith(instant);
        }

        await next(context).ConfigureAwait(false);
    }
}
