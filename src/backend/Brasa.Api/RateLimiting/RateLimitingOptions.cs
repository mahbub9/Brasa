namespace Brasa.Api.RateLimiting;

/// <summary>
/// Fixed-window rate limit applied per <c>(tenant, X-Brasa-Client client id)</c>
/// partition on every <c>/api/**</c> request (API-12).
/// </summary>
/// <remarks>
/// <c>docs/architecture/api-contract.md</c> lists per-platform rate limits
/// as deliberately deferred, but names exactly why they're cheap to add
/// later: "the <c>X-Brasa-Client</c> header makes them addable without a
/// contract change." This is that addition. Config-bound under
/// <c>RateLimiting</c>, with defaults generous enough not to interfere with
/// real service — tune per deployment without a code change.
/// </remarks>
public sealed class RateLimitingOptions
{
    /// <summary>Requests allowed per window, per partition.</summary>
    public int PermitLimit { get; init; } = 1000;

    /// <summary>Window length, in seconds.</summary>
    public int WindowSeconds { get; init; } = 60;
}
