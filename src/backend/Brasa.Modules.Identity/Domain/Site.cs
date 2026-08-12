using Brasa.Shared.Persistence;
using Brasa.Shared.Time;

namespace Brasa.Modules.Identity.Domain;

/// <summary>
/// A physical restaurant location belonging to an <see cref="Organization"/>
/// (IDN-01) — the middle tier a chain's separate addresses need, and the
/// thing CAT-05's price lists and FLR-06's waiter sections will key by once
/// built.
/// </summary>
/// <remarks>
/// <see cref="Region"/> carries a real <see cref="PortugueseRegion"/> from
/// the start, not a placeholder — the Azores are an hour behind the
/// mainland (see <c>PortugueseTimeZone</c>'s own remarks), and a chain
/// spanning regions needs each site to say which one it is in before
/// anything (daily close, SAF-T boundaries) can pick the right clock.
/// Nothing reads this yet — today's fiscal/reporting code still assumes
/// mainland time everywhere, an honest gap named at each call site, not
/// silently fixed by this row's mere existence.
/// </remarks>
public sealed class Site : Entity
{
    private Site()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new site under an organization.</summary>
    public Site(Guid organizationId, string name, PortugueseRegion region)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Site name must not be empty.", nameof(name));
        }

        OrganizationId = organizationId;
        Name = name.Trim();
        Region = region;
    }

    /// <summary>The organization this site belongs to.</summary>
    public Guid OrganizationId { get; private set; }

    /// <summary>Display name, e.g. "Restaurante Central — Chiado".</summary>
    public string Name { get; private set; }

    /// <summary>Which Portuguese region this site is in — mainland, Madeira or Açores.</summary>
    public PortugueseRegion Region { get; private set; }
}
