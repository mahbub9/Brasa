namespace Brasa.Shared.Time;

/// <summary>
/// A per-request-overridable <see cref="IClock"/> (QA-04). Falls back to the
/// real system clock until <see cref="OverrideWith"/> is called.
/// </summary>
/// <remarks>
/// <para>
/// The override lives in a static <see cref="AsyncLocal{T}"/>, not instance
/// state — the same reason <c>TenantContextAccessor</c> uses one instead of
/// a scoped service: <c>MockFiscalProvider</c> (and, later, the real
/// fiscal provider) is deliberately <b>singleton</b>-lifetime, since its
/// in-memory sequential document numbering must survive across requests,
/// not reset every time. A singleton cannot consume a scoped
/// <see cref="IClock"/> — ASP.NET Core's own DI validation refuses to start
/// the app if it tries (a real "captive dependency" caught this exact way
/// while building this type; see the trap in <c>docs/ai/README.md</c>). An
/// <see cref="AsyncLocal{T}"/> gives the same per-request isolation a
/// scoped service would, without constraining every consumer's own
/// lifetime: ASP.NET Core runs each request on its own async call chain, so
/// one request's override can never leak into a concurrently-processing
/// one — the same guarantee two of Playwright's parallel workers hitting
/// this one running API instance both need.
/// </para>
/// <para>
/// The only thing that ever calls <see cref="OverrideWith"/> is
/// <c>Brasa.Api.Testing.TestClockMiddleware</c>, which is wired into the
/// pipeline only when <c>!IsProduction()</c> — the same guard
/// <c>DevTenantMiddleware</c> uses. A production deployment never registers
/// that middleware, so nothing in production can ever call this, even
/// though <see cref="TestableClock"/> itself stays registered everywhere
/// (harmless: with nothing to call <see cref="OverrideWith"/>, it is
/// indistinguishable from <see cref="SystemClock"/>).
/// </para>
/// </remarks>
public sealed class TestableClock : IClock
{
    private static readonly AsyncLocal<DateTimeOffset?> OverrideUtcNow = new();

    private readonly SystemClock _systemClock = new();

    /// <inheritdoc/>
    public DateTimeOffset UtcNow => OverrideUtcNow.Value ?? _systemClock.UtcNow;

    /// <summary>Fixes every <see cref="TestableClock"/>'s <see cref="UtcNow"/> to <paramref name="instant"/> for the rest of this request's own async call chain.</summary>
    public static void OverrideWith(DateTimeOffset instant) => OverrideUtcNow.Value = instant;
}
