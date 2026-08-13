using Brasa.Shared.Persistence;

namespace Brasa.Modules.Identity.Domain;

/// <summary>
/// A per-tenant, optionally per-platform on/off switch (IDN-16).
/// </summary>
/// <remarks>
/// <para>
/// One of the "scale decisions to make on day one" the project's own build
/// plan named explicitly — cheap now, expensive to retrofit once a real
/// consumer needs one mid-rollout. No consumer exists yet: no native app to
/// gate (MOB epic), no OAuth/tiering story to key a paid-tier feature off of
/// (IDN-02…05, IDN-13). This ships the mechanism only, the same
/// "mechanism before the trigger" shape CAT-05/CAT-10/CAT-16/FLR-05 already
/// established in this codebase — a real caller picks it up when one exists.
/// </para>
/// <para>
/// <see cref="Platform"/> is never null — <see cref="AllPlatforms"/> is the
/// explicit sentinel for "every platform" instead, so the
/// <c>(TenantId, Key, Platform)</c> uniqueness constraint can be a plain
/// database unique index. Postgres unique indexes treat every <c>NULL</c> as
/// distinct from every other <c>NULL</c>, so a nullable column here would
/// have silently let two "all platforms" rows for the same key coexist —
/// the DB backstop the same "domain guard plus a unique index" shape every
/// other uniqueness constraint in this codebase relies on would have quietly
/// stopped applying to the one case (no platform override) a flag is most
/// likely to actually be used in.
/// </para>
/// <para>
/// Free-form lowercase string, not a closed enum — the same shape
/// <c>ClientInfo.Platform</c> (API-06) already uses for the same concept,
/// deliberately not unified into one shared type: that one is parsed from a
/// client-supplied header and never persisted, this one is tenant
/// configuration and always is; coupling them would make an API client's
/// header vocabulary a schema migration away from a back-office admin's flag
/// vocabulary; the same platform name, in practice.
/// </para>
/// </remarks>
public sealed class FeatureFlag : Entity
{
    /// <summary>The sentinel <see cref="Platform"/> value meaning every platform, not one specific client.</summary>
    public const string AllPlatforms = "all";

    private FeatureFlag()
    {
        // EF Core materialisation.
        Key = string.Empty;
        Platform = AllPlatforms;
    }

    /// <summary>Creates a feature flag for a key, optionally scoped to one platform.</summary>
    public FeatureFlag(string key, string platform, bool isEnabled)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Feature flag key must not be empty.", nameof(key));
        }

        if (string.IsNullOrWhiteSpace(platform))
        {
            throw new ArgumentException("Feature flag platform must not be empty.", nameof(platform));
        }

        Key = key.Trim().ToLowerInvariant();
        Platform = platform.Trim().ToLowerInvariant();
        IsEnabled = isEnabled;
    }

    /// <summary>The flag's name, e.g. <c>"kds"</c>. Always lowercase.</summary>
    public string Key { get; private set; }

    /// <summary>Which platform this row applies to, e.g. <c>"web"</c>, <c>"ios"</c> — or <see cref="AllPlatforms"/>. Always lowercase.</summary>
    public string Platform { get; private set; }

    /// <summary>Whether the flag is currently on for this key/platform combination.</summary>
    public bool IsEnabled { get; private set; }

    /// <summary>Toggles the flag on or off.</summary>
    public void SetEnabled(bool isEnabled) => IsEnabled = isEnabled;
}
