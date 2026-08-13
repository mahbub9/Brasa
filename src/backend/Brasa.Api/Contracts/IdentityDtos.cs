using Brasa.Modules.Identity.Domain;

namespace Brasa.Api.Contracts;

/// <summary>The top of the Organization → Site → Terminal hierarchy (IDN-01).</summary>
public sealed record OrganizationDto(Guid Id, string Name);

/// <summary>
/// A physical restaurant location. <c>Region</c> is a <c>PortugueseRegion</c>
/// name, e.g. <c>"Continental"</c>, <c>"Madeira"</c> or <c>"Azores"</c>.
/// </summary>
public sealed record SiteDto(Guid Id, Guid OrganizationId, string Name, string Region);

/// <summary>A physical POS device registered at a site. No pairing/auth yet — IDN-07.</summary>
public sealed record TerminalDto(Guid Id, Guid SiteId, string Label);

/// <summary>
/// A staff member who can sign in with a PIN (IDN-08/09). <c>Role</c> is a
/// <c>StaffRole</c> name (<c>"Staff"</c> or <c>"Manager"</c>). Never carries
/// the PIN or its hash — <c>IsLocked</c> is computed server-side so a client
/// never needs to compare <c>LockedUntilUtc</c> against its own clock.
/// </summary>
public sealed record StaffDto(Guid Id, Guid SiteId, string Name, string Role, bool IsLocked);

/// <summary>Request body to create an organization.</summary>
public sealed record CreateOrganizationRequest(string Name);

/// <summary>
/// Request body to create a site under an organization. <c>Region</c> is a
/// <c>PortugueseRegion</c> name.
/// </summary>
public sealed record CreateSiteRequest(string Name, string Region);

/// <summary>Request body to register a terminal at a site.</summary>
public sealed record CreateTerminalRequest(string Label);

/// <summary>Request body to create a staff member. <c>Role</c> is a <c>StaffRole</c> name; <c>Pin</c> is 4-6 digits.</summary>
public sealed record CreateStaffRequest(string Name, string Role, string Pin);

/// <summary>Request body to verify a staff member's PIN, or to set a new one.</summary>
public sealed record StaffPinRequest(string Pin);

/// <summary>Maps Identity domain entities to wire DTOs.</summary>
public static class IdentityDtoMappings
{
    public static OrganizationDto ToDto(this Organization organization) => new(organization.Id, organization.Name);

    public static SiteDto ToDto(this Site site) => new(site.Id, site.OrganizationId, site.Name, site.Region.ToString());

    public static TerminalDto ToDto(this Terminal terminal) => new(terminal.Id, terminal.SiteId, terminal.Label);

    public static StaffDto ToDto(this Staff staff, DateTimeOffset nowUtc) => new(
        staff.Id,
        staff.SiteId,
        staff.Name,
        staff.Role.ToString(),
        staff.LockedUntilUtc is { } lockedUntil && nowUtc < lockedUntil);
}
