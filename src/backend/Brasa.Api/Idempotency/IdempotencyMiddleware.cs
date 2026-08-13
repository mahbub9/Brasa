using Brasa.Shared.Primitives;
using Brasa.Shared.Tenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Primitives;

namespace Brasa.Api.Idempotency;

/// <summary>Cached response for a replayed idempotent request.</summary>
internal sealed record CachedResponse(int StatusCode, string ContentType, byte[] Body);

/// <summary>
/// Requires an <c>Idempotency-Key</c> header on every mutating request under
/// <c>/api</c>, and replays the original response verbatim on a repeat.
/// </summary>
/// <remarks>
/// <para>
/// Mobile networks fail mid-request constantly. A retried "close table" must not
/// produce two fiscal documents — this is API-05 from
/// <c>docs/architecture/api-contract.md</c>, and it is required, not optional,
/// from the very first mutating endpoint.
/// </para>
/// <para>
/// <b>I0 scope:</b> the cache is in-memory and per-instance. That is sufficient
/// for a single API instance and is exactly what it looks like — a durable,
/// Postgres-backed store is needed before running more than one instance, and is
/// tracked as a follow-up rather than silently assumed to already be safe for
/// that case.
/// </para>
/// <para>
/// A response is cached only when the status is below 500: a server error may
/// mean the operation never committed, so the caller must be free to retry as a
/// fresh attempt rather than replay a failure forever.
/// </para>
/// </remarks>
public sealed class IdempotencyMiddleware(RequestDelegate next, IMemoryCache cache)
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context, ITenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(tenantContext);

        if (!IsMutatingApiRequest(context))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var hasKey = context.Request.Headers.TryGetValue("Idempotency-Key", out var keyValues);
        if (!hasKey || StringValues.IsNullOrEmpty(keyValues))
        {
            await Error.Validation(
                    "request.idempotency_key_required",
                    "Mutating requests must carry an Idempotency-Key header.")
                .ToProblem()
                .ExecuteAsync(context)
                .ConfigureAwait(false);
            return;
        }

        // Scoped by tenant so two tenants can never collide on a client-chosen key.
        var cacheKey = $"idempotency:{tenantContext.TenantId:N}:{keyValues}";

        if (cache.TryGetValue(cacheKey, out CachedResponse? cached) && cached is not null)
        {
            context.Response.Headers["Idempotent-Replay"] = "true";
            context.Response.StatusCode = cached.StatusCode;
            context.Response.ContentType = cached.ContentType;

            // A 204 (or any other empty-bodied replay) must never reach WriteAsync
            // at all -- Kestrel treats even a zero-length write as "started a body"
            // and throws InvalidOperationException for a status that forbids one,
            // which crashes the whole connection, not just this request.
            if (cached.Body.Length > 0)
            {
                await context.Response.Body.WriteAsync(cached.Body).ConfigureAwait(false);
            }

            return;
        }

        var originalBody = context.Response.Body;
        await using var buffer = new MemoryStream();
        context.Response.Body = buffer;

        try
        {
            await next(context).ConfigureAwait(false);
        }
        finally
        {
            context.Response.Body = originalBody;
        }

        var bytes = buffer.ToArray();

        if (context.Response.StatusCode < StatusCodes.Status500InternalServerError)
        {
            cache.Set(
                cacheKey,
                new CachedResponse(context.Response.StatusCode, context.Response.ContentType ?? "application/json", bytes),
                CacheDuration);
        }

        // Same reasoning as the replay path above: skip the write entirely for an
        // empty body (204 and friends) rather than let Kestrel reject it.
        if (bytes.Length > 0)
        {
            await originalBody.WriteAsync(bytes).ConfigureAwait(false);
        }
    }

    private static bool IsMutatingApiRequest(HttpContext context)
        => context.Request.Path.StartsWithSegments("/api", StringComparison.Ordinal)
            && MutatingMethods.Contains(context.Request.Method);
}
