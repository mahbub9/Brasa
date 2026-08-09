namespace Brasa.Api.ClientVersioning;

/// <summary>
/// Configuration-bound version policy for one client id, under the
/// <c>ClientRequirements</c> section (API-07). Not the wire DTO —
/// <see cref="Brasa.Api.Contracts.ClientRequirementsDto"/> is what
/// <c>GET /client-requirements</c> actually returns.
/// </summary>
public sealed class ClientRequirementEntry
{
    /// <summary>Oldest version still allowed to call the API.</summary>
    public string MinimumSupported { get; init; } = string.Empty;

    /// <summary>Version the client should prompt the user to update to.</summary>
    public string Recommended { get; init; } = string.Empty;

    /// <summary>Date <see cref="MinimumSupported"/> stops being accepted, or null if none is scheduled yet.</summary>
    public string? SunsetAfter { get; init; }
}
