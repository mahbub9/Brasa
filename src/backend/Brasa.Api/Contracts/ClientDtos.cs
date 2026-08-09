namespace Brasa.Api.Contracts;

/// <summary>
/// <c>GET /client-requirements</c>'s response (API-07,
/// <c>docs/architecture/api-contract.md</c> §3) — lets a client decide for
/// itself whether to show "update required" or "update recommended",
/// without the backend having to guess what version is calling.
/// </summary>
public sealed record ClientRequirementsDto(string MinimumSupported, string Recommended, string? SunsetAfter);
