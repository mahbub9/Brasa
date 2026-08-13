using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Identity.Domain;

/// <summary>Which staff role a <see cref="Staff"/> member holds.</summary>
public enum StaffRole
{
    /// <summary>An ordinary staff member — waiter, kitchen, bar.</summary>
    Staff = 0,

    /// <summary>A manager — the eventual gate for privileged actions like voids and discounts (IDN-11, not built yet).</summary>
    Manager = 1,
}

/// <summary>
/// A staff member who can sign in with a PIN, scoped to a <see cref="Site"/>
/// — the same tier <see cref="Terminal"/> already sits at.
/// </summary>
/// <remarks>
/// <para>
/// A narrow first slice of two epic rows at once: IDN-09 (PIN hashing,
/// lockout, rotation policy) is what this type's own methods actually are;
/// IDN-08 (staff PIN sign-in) is only half-built here — verifying a PIN
/// against a *known* staff id, not "identify me by PIN alone with no
/// picker." That broader UX was deliberately not chosen: without knowing
/// which staff member is attempting, a failed PIN cannot be attributed to
/// the right person's lockout counter without either skipping lockout
/// tracking entirely for that path or incorrectly penalising every
/// non-matching staff member's own counter on someone else's mistake. See
/// the feature page for the full reasoning.
/// </para>
/// <para>
/// <b>Not wired into any endpoint's own authorization decision.</b>
/// ORD-10 (void) and ORD-11 (discount) both still have "no
/// manager-authorisation gate yet" as their own named, deferred gap —
/// this ships the verification mechanism itself, ready for whichever
/// future task (IDN-11) makes a privileged action actually require it.
/// No terminal pairing (IDN-07) and no OAuth/JWT session issuance
/// (IDN-03/04/05) either — <c>DevTenantMiddleware</c> remains the entire
/// auth story for every HTTP request until I3.
/// </para>
/// </remarks>
public sealed class Staff : Entity
{
    /// <summary>Consecutive incorrect PINs before a lockout kicks in.</summary>
    public const int MaxFailedAttempts = 5;

    /// <summary>How long a lockout lasts once triggered.</summary>
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private Staff()
    {
        // EF Core materialisation.
        Name = string.Empty;
        PinHash = string.Empty;
    }

    /// <summary>Creates a new staff member with an initial PIN.</summary>
    public Staff(Guid siteId, string name, StaffRole role, string pin)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Staff name must not be empty.", nameof(name));
        }

        if (!IsValidPinFormat(pin))
        {
            throw new ArgumentException("PIN must be 4-6 digits.", nameof(pin));
        }

        SiteId = siteId;
        Name = name.Trim();
        Role = role;
        PinHash = PinHasher.Hash(pin);
    }

    /// <summary>The site this staff member works at.</summary>
    public Guid SiteId { get; private set; }

    /// <summary>Display name.</summary>
    public string Name { get; private set; }

    /// <summary>Staff or Manager — see <see cref="StaffRole"/>.</summary>
    public StaffRole Role { get; private set; }

    /// <summary>PBKDF2 hash — see <see cref="PinHasher"/>. Never exposed on any DTO.</summary>
    private string PinHash { get; set; }

    /// <summary>Consecutive incorrect PIN attempts since the last success or rotation.</summary>
    public int FailedPinAttempts { get; private set; }

    /// <summary>Locked out until this instant, or <c>null</c> if not currently locked.</summary>
    public DateTimeOffset? LockedUntilUtc { get; private set; }

    /// <summary>A PIN must be 4–6 digits — long enough to not be trivially guessable at a shared terminal, short enough to type fast during service.</summary>
    public static bool IsValidPinFormat(string pin) => pin.Length is >= 4 and <= 6 && pin.All(char.IsAsciiDigit);

    /// <summary>
    /// Verifies a PIN against this staff member's own hash. Locks out after
    /// <see cref="MaxFailedAttempts"/> consecutive failures; a correct PIN
    /// always resets the counter, including one entered while still inside
    /// an active lockout window's own final second.
    /// </summary>
    public Result VerifyPin(string pin, DateTimeOffset nowUtc)
    {
        if (LockedUntilUtc is { } lockedUntil && nowUtc < lockedUntil)
        {
            return Result.Failure(Error.Conflict(
                "identity.staff_locked", $"{Name} is locked out until {lockedUntil:O} after too many incorrect PINs."));
        }

        if (!PinHasher.Verify(pin, PinHash))
        {
            FailedPinAttempts++;
            if (FailedPinAttempts >= MaxFailedAttempts)
            {
                LockedUntilUtc = nowUtc + LockoutDuration;
            }

            return Result.Failure(Error.Validation("identity.pin_incorrect", "Incorrect PIN."));
        }

        FailedPinAttempts = 0;
        LockedUntilUtc = null;
        return Result.Success();
    }

    /// <summary>Rotates this staff member's PIN, clearing any lockout — a fresh start, not a way to talk your way out of one mid-lockout with the same compromised PIN.</summary>
    public Result SetPin(string newPin)
    {
        if (!IsValidPinFormat(newPin))
        {
            return Result.Failure(Error.Validation("identity.invalid_pin", "PIN must be 4-6 digits."));
        }

        PinHash = PinHasher.Hash(newPin);
        FailedPinAttempts = 0;
        LockedUntilUtc = null;
        return Result.Success();
    }
}
