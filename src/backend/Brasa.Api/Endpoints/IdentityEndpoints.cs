using Brasa.Api.Contracts;
using Brasa.Modules.Identity.Domain;
using Brasa.Modules.Identity.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Organization / Site / Terminal registry endpoints (IDN-01) — a narrow
/// first slice of the Identity epic. Create and list only; no update or
/// delete yet, and no pairing/auth (IDN-06/07 are separate, not-yet-built
/// rows). Exists so <c>Site</c> has a stable, referenceable id — CAT-05
/// (price lists per site) and FLR-06 (waiter section assignment) are the
/// intended near-term consumers, neither built yet.
/// </summary>
public static class IdentityEndpoints
{
    /// <summary>Maps the identity endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapIdentityEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/organizations", CreateOrganizationAsync)
            .WithName("CreateOrganization")
            .WithSummary("Creates an organization — the top of the Organization/Site/Terminal hierarchy (IDN-01).");

        group.MapGet("/organizations", GetOrganizationsAsync)
            .WithName("GetOrganizations")
            .WithSummary("Lists every organization for the current tenant.");

        group.MapPost("/organizations/{organizationId:guid}/sites", CreateSiteAsync)
            .WithName("CreateSite")
            .WithSummary("Creates a site (a physical restaurant location) under an organization (IDN-01).");

        group.MapGet("/organizations/{organizationId:guid}/sites", GetSitesAsync)
            .WithName("GetSites")
            .WithSummary("Lists every site under an organization.");

        group.MapPost("/sites/{siteId:guid}/terminals", CreateTerminalAsync)
            .WithName("CreateTerminal")
            .WithSummary("Registers a terminal (a physical POS device) at a site (IDN-01). No pairing/auth yet.");

        group.MapGet("/sites/{siteId:guid}/terminals", GetTerminalsAsync)
            .WithName("GetTerminals")
            .WithSummary("Lists every terminal registered at a site.");

        return group;
    }

    private static async Task<IResult> CreateOrganizationAsync(
        CreateOrganizationRequest request,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("identity.invalid_organization_name", "Organization name must not be empty.").ToProblem();
        }

        var organization = new Organization(request.Name.Trim());
        db.Organizations.Add(organization);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(organization.ToDto());
    }

    private static async Task<IResult> GetOrganizationsAsync(IdentityDbContext db, CancellationToken cancellationToken)
    {
        var organizations = await db.Organizations
            .OrderBy(o => o.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(organizations.Select(o => o.ToDto()).ToList());
    }

    private static async Task<IResult> CreateSiteAsync(
        Guid organizationId,
        CreateSiteRequest request,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var organization = await db.Organizations
            .FirstOrDefaultAsync(o => o.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (organization is null)
        {
            return Error.NotFound("identity.organization_not_found", $"Organization {organizationId} was not found.").ToProblem();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("identity.invalid_site_name", "Site name must not be empty.").ToProblem();
        }

        var regionResult = ParseRegion(request.Region);
        if (regionResult.IsFailure)
        {
            return regionResult.Error.ToProblem();
        }

        var site = new Site(organizationId, request.Name.Trim(), regionResult.Value);
        db.Sites.Add(site);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(site.ToDto());
    }

    private static async Task<IResult> GetSitesAsync(
        Guid organizationId,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var organizationExists = await db.Organizations
            .AnyAsync(o => o.Id == organizationId, cancellationToken)
            .ConfigureAwait(false);

        if (!organizationExists)
        {
            return Error.NotFound("identity.organization_not_found", $"Organization {organizationId} was not found.").ToProblem();
        }

        var sites = await db.Sites
            .Where(s => s.OrganizationId == organizationId)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(sites.Select(s => s.ToDto()).ToList());
    }

    private static async Task<IResult> CreateTerminalAsync(
        Guid siteId,
        CreateTerminalRequest request,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var site = await db.Sites
            .FirstOrDefaultAsync(s => s.Id == siteId, cancellationToken)
            .ConfigureAwait(false);

        if (site is null)
        {
            return Error.NotFound("identity.site_not_found", $"Site {siteId} was not found.").ToProblem();
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Error.Validation("identity.invalid_terminal_label", "Terminal label must not be empty.").ToProblem();
        }

        var terminal = new Terminal(siteId, request.Label.Trim());
        db.Terminals.Add(terminal);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(terminal.ToDto());
    }

    private static async Task<IResult> GetTerminalsAsync(
        Guid siteId,
        IdentityDbContext db,
        CancellationToken cancellationToken)
    {
        var siteExists = await db.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false);
        if (!siteExists)
        {
            return Error.NotFound("identity.site_not_found", $"Site {siteId} was not found.").ToProblem();
        }

        var terminals = await db.Terminals
            .Where(t => t.SiteId == siteId)
            .OrderBy(t => t.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(terminals.Select(t => t.ToDto()).ToList());
    }

    private static Result<PortugueseRegion> ParseRegion(string region)
    {
        return Enum.TryParse<PortugueseRegion>(region, ignoreCase: true, out var parsed)
            ? Result.Success(parsed)
            : Result.Failure<PortugueseRegion>(
                Error.Validation("identity.invalid_region", $"\"{region}\" is not a recognised Portuguese region."));
    }
}
