namespace Brasa.Api.ClientVersioning;

/// <summary>
/// RFC 8594 <c>Deprecation</c>/<c>Sunset</c> response headers for the whole
/// <c>/api/v1</c> surface (API-08, <c>docs/architecture/api-contract.md</c>
/// §3's version policy: "Deprecations are announced with RFC 8594
/// <c>Deprecation</c> and <c>Sunset</c> response headers before removal").
/// Config-bound under <c>Api:Deprecation</c> and empty by default — nothing
/// is configured until a real <c>/api/v2</c> exists and v1 is actually
/// scheduled for removal.
/// </summary>
public sealed class ApiDeprecationOptions
{
    /// <summary>When v1 was marked deprecated, or null if it isn't.</summary>
    public DateTimeOffset? DeprecatedAt { get; init; }

    /// <summary>When v1 stops being served, or null if no date is scheduled yet.</summary>
    public DateTimeOffset? SunsetAt { get; init; }

    /// <summary>Optional migration-guidance URL, sent as a <c>Link</c> header (<c>rel="sunset"</c>) per RFC 8594 §5.2.</summary>
    public string? Link { get; init; }
}
