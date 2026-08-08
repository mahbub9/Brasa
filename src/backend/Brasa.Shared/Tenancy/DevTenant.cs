namespace Brasa.Shared.Tenancy;

/// <summary>
/// The single hardcoded tenant used before real authentication exists.
/// </summary>
/// <remarks>
/// <para>
/// I0's walking skeleton (<c>docs/product/roadmap.md</c>) deliberately ships
/// with no auth — that is IDN-03…08, landing in I3. Every request until then is
/// attributed to this fixed tenant by a dev-only middleware, so the rest of the
/// stack (tenancy, RLS, the domain) is exercised for real while auth does not
/// yet exist.
/// </para>
/// <para>
/// This is <b>not</b> a shortcut on multi-tenancy itself — RLS, the query
/// filters and the schema are all real. It is a shortcut only on <i>who the
/// tenant is</i>, and it is guarded so it can never reach a real deployment: see
/// <c>DevTenantMiddleware</c>'s Production check in <c>Brasa.Api</c>.
/// </para>
/// </remarks>
public static class DevTenant
{
    /// <summary>Fixed id for the single dev/demo tenant. Never used once IDN-03…08 land.</summary>
    public static readonly Guid Id = Guid.Parse("00000000-0000-7000-8000-000000000001");

    /// <summary>Display name for the seeded dev tenant.</summary>
    public const string Name = "Brasa Demo";
}
