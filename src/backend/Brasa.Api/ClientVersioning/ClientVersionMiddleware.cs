using Serilog.Context;

namespace Brasa.Api.ClientVersioning;

/// <summary>
/// Parses the <c>X-Brasa-Client</c> header (API-06,
/// <c>docs/architecture/api-contract.md</c> §3) and makes it available to
/// endpoints via <see cref="HttpContext.Items"/>, and enriches every log
/// line written during the request with the caller's client id, version
/// and platform.
/// </summary>
/// <remarks>
/// Deliberately best-effort: no client sends this header yet (it exists
/// ahead of any client that does, the same way CAT-02/CAT-18 shipped ahead
/// of the admin UI that will call them), so a missing or malformed value
/// must never fail the request. <c>GET /client-requirements</c> (API-07) is
/// the one endpoint that actually depends on it, and rejects its own
/// absence explicitly there — this middleware does not gate every request
/// on a header almost nothing sends today.
/// </remarks>
public sealed class ClientVersionMiddleware(RequestDelegate next)
{
    /// <summary>Key under which the parsed <see cref="ClientInfo"/> is stored in <see cref="HttpContext.Items"/>, when present.</summary>
    public const string HttpContextItemKey = "Brasa.ClientInfo";

    /// <summary>Invokes the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ClientInfo.TryParse(context.Request.Headers["X-Brasa-Client"], out var info) || info is null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        context.Items[HttpContextItemKey] = info;

        using (LogContext.PushProperty("ClientId", info.ClientId))
        using (LogContext.PushProperty("ClientVersion", info.Version))
        using (LogContext.PushProperty("ClientPlatform", info.Platform))
        {
            await next(context).ConfigureAwait(false);
        }
    }
}
