using Brasa.Shared.Persistence;

namespace Brasa.Modules.Identity.Domain;

/// <summary>
/// The top of the Organization → Site → Terminal hierarchy (IDN-01) — the
/// restaurant business itself, as distinct from <c>TenantId</c>, the opaque
/// partition key every table already carries. A tenant has at least one
/// Organization; nothing here assumes exactly one, since a single signed-up
/// customer running more than one separate brand under one account is not
/// ruled out, just not a scenario anything currently exercises.
/// </summary>
/// <remarks>
/// A narrow first slice: no user/staff assignment, no billing, no settings —
/// those are separate, not-yet-built IDN rows. This exists so <c>Site</c> has
/// a stable parent to hang off of, and so CAT-05 (price lists per site) and
/// FLR-06 (waiter section assignment) have a real <c>SiteId</c> to key by.
/// </remarks>
public sealed class Organization : Entity
{
    private Organization()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new organization.</summary>
    public Organization(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Organization name must not be empty.", nameof(name));
        }

        Name = name.Trim();
    }

    /// <summary>Display name, e.g. "Restaurante Central, Lda".</summary>
    public string Name { get; private set; }
}
