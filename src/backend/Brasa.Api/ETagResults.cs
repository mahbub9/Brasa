using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace Brasa.Api;

/// <summary>
/// <c>ETag</c> / <c>If-None-Match</c> support for read endpoints whose data
/// changes rarely (API-10, <c>docs/architecture/api-contract.md</c> §9) — a
/// menu pull is the canonical case: most requests from a POS that already
/// has the current menu should come back as a bodyless <c>304</c>, not the
/// same few kilobytes of JSON every time.
/// </summary>
/// <remarks>
/// Deliberately not applied to <c>GET /floor</c>: table state changes
/// continuously through service, so almost every request would be a genuine
/// cache miss — the complexity would buy nothing. ETags belong on data that
/// is mostly stable, which is exactly what this type's name and the
/// contract doc both say: "configuration and menu pulls."
/// </remarks>
public static class ETagResults
{
    /// <summary>
    /// Serialises <paramref name="value"/>, computes a strong <c>ETag</c>
    /// from its bytes, and returns a bodyless <c>304 Not Modified</c> if the
    /// request's <c>If-None-Match</c> already matches — otherwise <c>200</c>
    /// with the body and the computed <c>ETag</c> header.
    /// </summary>
    public static IResult OkWithETag<T>(HttpContext context, T value)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Must match the serializer Results.Ok(...) would have used —
        // ASP.NET Core's configured options (camelCase), not
        // JsonSerializer's own PascalCase default. Using the wrong one here
        // silently changed every field name in the response body.
        var serializerOptions = context.RequestServices
            .GetRequiredService<IOptions<JsonOptions>>().Value.SerializerOptions;

        var json = JsonSerializer.SerializeToUtf8Bytes(value, serializerOptions);
        var hash = Convert.ToHexString(SHA256.HashData(json));
        var etag = $"\"{hash}\"";

        context.Response.Headers.ETag = etag;

        if (context.Request.Headers.IfNoneMatch == etag)
        {
            return Results.StatusCode(StatusCodes.Status304NotModified);
        }

        return Results.Bytes(json, "application/json");
    }
}
