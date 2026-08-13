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
            .WithSummary("Creates an organization — the top of the Organization/Site/Terminal hierarchy (IDN-01).")
            .Produces<OrganizationDto>();

        group.MapGet("/organizations", GetOrganizationsAsync)
            .WithName("GetOrganizations")
            .WithSummary("Lists every organization for the current tenant.")
            .Produces<List<OrganizationDto>>();

        group.MapPost("/organizations/{organizationId:guid}/sites", CreateSiteAsync)
            .WithName("CreateSite")
            .WithSummary("Creates a site (a physical restaurant location) under an organization (IDN-01).")
            .Produces<SiteDto>();

        group.MapGet("/organizations/{organizationId:guid}/sites", GetSitesAsync)
            .WithName("GetSites")
            .WithSummary("Lists every site under an organization.")
            .Produces<List<SiteDto>>();

        group.MapPost("/sites/{siteId:guid}/terminals", CreateTerminalAsync)
            .WithName("CreateTerminal")
            .WithSummary("Registers a terminal (a physical POS device) at a site (IDN-01). No pairing/auth yet.")
            .Produces<TerminalDto>();

        group.MapGet("/sites/{siteId:guid}/terminals", GetTerminalsAsync)
            .WithName("GetTerminals")
            .WithSummary("Lists every terminal registered at a site.")
            .Produces<List<TerminalDto>>();

        group.MapPost("/sites/{siteId:guid}/staff", CreateStaffAsync)
            .WithName("CreateStaff")
            .WithSummary("Creates a staff member with an initial PIN at a site (IDN-08/09).")
            .Produces<StaffDto>();

        group.MapGet("/sites/{siteId:guid}/staff", GetStaffAsync)
            .WithName("GetStaff")
            .WithSummary("Lists every staff member at a site.")
            .Produces<List<StaffDto>>();

        group.MapPost("/staff/{staffId:guid}/verify-pin", VerifyStaffPinAsync)
            .WithName("VerifyStaffPin")
            .WithSummary("Verifies a staff member's PIN. Locks the account out after 5 consecutive incorrect PINs.")
            .Produces<StaffDto>();

        group.MapPut("/staff/{staffId:guid}/pin", SetStaffPinAsync)
            .WithName("SetStaffPin")
            .WithSummary("Sets a staff member's PIN, clearing any lockout — an admin reset, not a self-service change.")
            .Produces<StaffDto>();

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

    private static async Task<IResult> CreateStaffAsync(
        Guid siteId,
        CreateStaffRequest request,
        IdentityDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var siteExists = await db.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false);
        if (!siteExists)
        {
            return Error.NotFound("identity.site_not_found", $"Site {siteId} was not found.").ToProblem();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("identity.invalid_staff_name", "Staff name must not be empty.").ToProblem();
        }

        var roleResult = ParseRole(request.Role);
        if (roleResult.IsFailure)
        {
            return roleResult.Error.ToProblem();
        }

        if (!Staff.IsValidPinFormat(request.Pin))
        {
            return Error.Validation("identity.invalid_pin", "PIN must be 4-6 digits.").ToProblem();
        }

        var staff = new Staff(siteId, request.Name.Trim(), roleResult.Value, request.Pin);
        db.Staff.Add(staff);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(staff.ToDto(clock.UtcNow));
    }

    private static async Task<IResult> GetStaffAsync(
        Guid siteId,
        IdentityDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var siteExists = await db.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false);
        if (!siteExists)
        {
            return Error.NotFound("identity.site_not_found", $"Site {siteId} was not found.").ToProblem();
        }

        var staff = await db.Staff
            .Where(s => s.SiteId == siteId)
            .OrderBy(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(staff.Select(s => s.ToDto(clock.UtcNow)).ToList());
    }

    private static async Task<IResult> VerifyStaffPinAsync(
        Guid staffId,
        StaffPinRequest request,
        IdentityDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var staff = await FindStaffAsync(db, staffId, cancellationToken).ConfigureAwait(false);
        if (staff is null)
        {
            return StaffNotFound(staffId).ToProblem();
        }

        var verifyResult = staff.VerifyPin(request.Pin, clock.UtcNow);
        // Persist regardless of outcome — a failed attempt still advances
        // FailedPinAttempts (and possibly triggers a lockout), which must
        // survive past this request exactly like a successful attempt's
        // reset must.
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (verifyResult.IsFailure)
        {
            return verifyResult.Error.ToProblem();
        }

        return Results.Ok(staff.ToDto(clock.UtcNow));
    }

    private static async Task<IResult> SetStaffPinAsync(
        Guid staffId,
        StaffPinRequest request,
        IdentityDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var staff = await FindStaffAsync(db, staffId, cancellationToken).ConfigureAwait(false);
        if (staff is null)
        {
            return StaffNotFound(staffId).ToProblem();
        }

        var setPinResult = staff.SetPin(request.Pin);
        if (setPinResult.IsFailure)
        {
            return setPinResult.Error.ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(staff.ToDto(clock.UtcNow));
    }

    private static Task<Staff?> FindStaffAsync(IdentityDbContext db, Guid staffId, CancellationToken cancellationToken)
        => db.Staff.FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken);

    private static Error StaffNotFound(Guid staffId)
        => Error.NotFound("identity.staff_not_found", $"Staff member {staffId} was not found.");

    private static Result<PortugueseRegion> ParseRegion(string region)
    {
        return Enum.TryParse<PortugueseRegion>(region, ignoreCase: true, out var parsed)
            ? Result.Success(parsed)
            : Result.Failure<PortugueseRegion>(
                Error.Validation("identity.invalid_region", $"\"{region}\" is not a recognised Portuguese region."));
    }

    private static Result<StaffRole> ParseRole(string role)
    {
        return Enum.TryParse<StaffRole>(role, ignoreCase: true, out var parsed)
            ? Result.Success(parsed)
            : Result.Failure<StaffRole>(
                Error.Validation("identity.invalid_staff_role", $"\"{role}\" is not a recognised staff role."));
    }
}
