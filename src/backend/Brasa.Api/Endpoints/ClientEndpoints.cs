using Brasa.Api.ClientVersioning;
using Brasa.Api.Contracts;
using Brasa.Shared.Primitives;
using Microsoft.Extensions.Options;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Client version negotiation (API-06/07, <c>docs/architecture/api-contract.md</c>
/// §3) — ships ahead of any client that actually sends <c>X-Brasa-Client</c>
/// or calls this endpoint, the same way CAT-02/CAT-18 shipped ahead of the
/// admin UI that will eventually call them.
/// </summary>
public static class ClientEndpoints
{
    /// <summary>Maps the client endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapClientEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/client-requirements", GetClientRequirementsAsync)
            .WithName("GetClientRequirements")
            .WithSummary("Minimum/recommended version and sunset date for the calling client (API-07).")
            .Produces<ClientRequirementsDto>();

        return group;
    }

    private static IResult GetClientRequirementsAsync(
        HttpContext httpContext,
        IOptions<Dictionary<string, ClientRequirementEntry>> requirements)
    {
        if (httpContext.Items[ClientVersionMiddleware.HttpContextItemKey] is not ClientInfo client)
        {
            return Error.Validation(
                "client.header_required",
                "GET /client-requirements requires a valid X-Brasa-Client header.").ToProblem();
        }

        if (!requirements.Value.TryGetValue(client.ClientId, out var entry))
        {
            return Error.NotFound(
                "client.unknown_client_id",
                $"No version requirements are configured for client \"{client.ClientId}\".").ToProblem();
        }

        return Results.Ok(new ClientRequirementsDto(entry.MinimumSupported, entry.Recommended, entry.SunsetAfter));
    }
}
