using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// A cash session — <i>abertura de caixa</i> (PAY-08) — a staff member
/// declaring a starting cash float against a specific terminal at the
/// start of a shift.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purely a record of the declaration — does not gate anything else
/// yet.</b> No payment endpoint requires an open session to exist, the
/// same "mechanism before the trigger" shape this codebase already uses
/// everywhere (<c>TaxRule</c> not wired into <c>AddLine</c>, price lists
/// not resolved through it, DAT-07's role with no real consumer yet).
/// Counting the float back out and reconciling variance at the end of a
/// shift is PAY-10/11's own later task, not this.
/// </para>
/// <para>
/// <c>TerminalId</c> and <c>OpenedByStaffId</c> are both plain opaque
/// references, the same convention <see cref="Payment.OrderId"/> already
/// uses for a Floor <c>Table</c> — Payments never queries Identity
/// directly (see <c>docs/architecture/module-boundaries.md</c>); the API
/// layer confirms both are real before this is ever constructed.
/// </para>
/// <para>
/// <b>Only one open session per terminal at a time.</b> Enforced by the
/// API layer, not this type — a second <c>Open</c> against a terminal that
/// already has one is rejected with <c>409 cash_session.already_open</c>
/// before a second <see cref="CashSession"/> is ever constructed. Two
/// overlapping sessions on the same till would make "who's responsible for
/// this cash drawer right now" ambiguous, the entire point of this record.
/// </para>
/// </remarks>
public sealed class CashSession : Entity
{
    private CashSession()
    {
        // EF Core materialisation.
    }

    /// <summary>Opens a cash session against a terminal.</summary>
    /// <param name="terminalId">The terminal this session is opened against. Never validated here — see the class remarks.</param>
    /// <param name="openedByStaffId">Which staff member declared the float. Never validated here — see the class remarks.</param>
    /// <param name="openingFloat">The starting cash amount in the drawer. Must not be negative.</param>
    /// <param name="openedAtUtc">When the session was opened.</param>
    public CashSession(Guid terminalId, Guid openedByStaffId, Money openingFloat, DateTimeOffset openedAtUtc)
    {
        if (terminalId == Guid.Empty)
        {
            throw new ArgumentException("Terminal id must not be empty.", nameof(terminalId));
        }

        if (openedByStaffId == Guid.Empty)
        {
            throw new ArgumentException("Opened-by staff id must not be empty.", nameof(openedByStaffId));
        }

        if (openingFloat.IsNegative)
        {
            throw new ArgumentException("Opening float must not be negative.", nameof(openingFloat));
        }

        TerminalId = terminalId;
        OpenedByStaffId = openedByStaffId;
        OpeningFloat = openingFloat;
        OpenedAtUtc = openedAtUtc;
    }

    /// <summary>The terminal this session is opened against.</summary>
    public Guid TerminalId { get; private set; }

    /// <summary>Which staff member declared the float.</summary>
    public Guid OpenedByStaffId { get; private set; }

    /// <summary>The starting cash amount declared in the drawer.</summary>
    public Money OpeningFloat { get; private set; }

    /// <summary>When this session was opened.</summary>
    public DateTimeOffset OpenedAtUtc { get; private set; }

    /// <summary>When this session was closed, if it has been.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    /// <summary>True until <see cref="Close"/> succeeds.</summary>
    public bool IsOpen => ClosedAtUtc is null;

    /// <summary>Closes the session. A bare status flip — variance reporting is PAY-10/11, not this.</summary>
    /// <param name="closedAtUtc">When the session was closed.</param>
    public Result Close(DateTimeOffset closedAtUtc)
    {
        if (!IsOpen)
        {
            return Result.Failure(Error.Validation(
                "cash_session.already_closed", "This cash session is already closed."));
        }

        ClosedAtUtc = closedAtUtc;
        return Result.Success();
    }
}
