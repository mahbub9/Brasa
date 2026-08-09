using System.Text.RegularExpressions;

namespace Brasa.Api.ClientVersioning;

/// <summary>
/// A parsed <c>X-Brasa-Client</c> header (API-06,
/// <c>docs/architecture/api-contract.md</c> §3):
/// <c>&lt;client-id&gt;/&lt;version&gt; (&lt;platform&gt;)</c>, e.g.
/// <c>pos-web/0.0.0 (web)</c>.
/// </summary>
public sealed record ClientInfo(string ClientId, string Version, string Platform)
{
    private static readonly Regex HeaderPattern = new(
        @"^(?<clientId>[a-z0-9-]+)/(?<version>\S+)\s*\((?<platform>[a-z0-9-]+)\)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>
    /// Parses an <c>X-Brasa-Client</c> header value. Never throws — no
    /// client sends this header yet, so an absent or malformed value is the
    /// expected common case today, not an exceptional one.
    /// </summary>
    public static bool TryParse(string? headerValue, out ClientInfo? info)
    {
        info = null;
        if (string.IsNullOrWhiteSpace(headerValue))
        {
            return false;
        }

        var match = HeaderPattern.Match(headerValue.Trim());
        if (!match.Success)
        {
            return false;
        }

        info = new ClientInfo(
            match.Groups["clientId"].Value,
            match.Groups["version"].Value,
            match.Groups["platform"].Value);
        return true;
    }
}
