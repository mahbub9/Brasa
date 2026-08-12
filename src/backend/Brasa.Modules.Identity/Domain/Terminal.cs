using Brasa.Shared.Persistence;

namespace Brasa.Modules.Identity.Domain;

/// <summary>
/// A physical POS device at a <see cref="Site"/> (IDN-01) — the bottom tier
/// of the Organization → Site → Terminal hierarchy. A bare registry entry
/// only: pairing (IDN-07), staff PIN sign-in (IDN-08) and everything else
/// that makes a terminal an authenticated actor are separate, not-yet-built
/// rows. Nothing in <c>pos</c>/<c>admin</c> reads or writes this today — the
/// single-tenant dev setup (<c>DevTenantMiddleware</c>) doesn't distinguish
/// terminals at all yet.
/// </summary>
public sealed class Terminal : Entity
{
    private Terminal()
    {
        // EF Core materialisation.
        Label = string.Empty;
    }

    /// <summary>Creates a new terminal registry entry at a site.</summary>
    public Terminal(Guid siteId, string label)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            throw new ArgumentException("Terminal label must not be empty.", nameof(label));
        }

        SiteId = siteId;
        Label = label.Trim();
    }

    /// <summary>The site this terminal is located at.</summary>
    public Guid SiteId { get; private set; }

    /// <summary>Display label, e.g. "Caixa 1" or "Tablet Sala".</summary>
    public string Label { get; private set; }
}
