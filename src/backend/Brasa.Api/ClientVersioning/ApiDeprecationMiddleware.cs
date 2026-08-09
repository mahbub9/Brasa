using System.Globalization;
using Microsoft.Extensions.Options;

namespace Brasa.Api.ClientVersioning;

/// <summary>
/// Adds RFC 8594 <c>Deprecation</c>/<c>Sunset</c> headers to every response
/// from <see cref="ApiDeprecationOptions"/> (API-08). A no-op today, the
/// same "ship the seam ahead of the trigger" shape as
/// <see cref="ClientVersionMiddleware"/> (API-06/07) — nothing is
/// configured until a real <c>/api/v2</c> exists.
/// </summary>
/// <remarks>
/// Headers are set unconditionally from config, with no clock-based
/// "already past sunset" logic: while <c>/api/v1</c> still serves a
/// request at all, RFC 8594 headers on it are correct regardless of
/// whether <see cref="ApiDeprecationOptions.SunsetAt"/> has already
/// passed — what happens *after* removal is a routing/removal decision
/// this middleware has no part in.
/// </remarks>
public sealed class ApiDeprecationMiddleware(RequestDelegate next, IOptions<ApiDeprecationOptions> options)
{
    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var deprecation = options.Value;

        if (deprecation.DeprecatedAt is { } deprecatedAt)
        {
            context.Response.Headers.Append("Deprecation", FormatHttpDate(deprecatedAt));
        }

        if (deprecation.SunsetAt is { } sunsetAt)
        {
            context.Response.Headers.Append("Sunset", FormatHttpDate(sunsetAt));
        }

        if (!string.IsNullOrWhiteSpace(deprecation.Link))
        {
            context.Response.Headers.Append("Link", $"<{deprecation.Link}>; rel=\"sunset\"");
        }

        await next(context).ConfigureAwait(false);
    }

    /// <summary>RFC 7231 IMF-fixdate, e.g. "Sun, 06 Nov 1994 08:49:37 GMT" — what both headers require.</summary>
    private static string FormatHttpDate(DateTimeOffset value)
        => value.UtcDateTime.ToString("R", CultureInfo.InvariantCulture);
}
