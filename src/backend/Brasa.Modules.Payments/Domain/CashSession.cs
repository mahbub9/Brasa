using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// A cash session — <i>abertura de caixa</i> (PAY-08) — a staff member
/// declaring a starting cash float against a specific terminal at the
/// start of a shift, a blind count of what's in the drawer at the end of
/// it (PAY-10), and eventually a close.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purely a record — does not gate anything else yet.</b> No payment
/// endpoint requires an open session to exist, the same "mechanism before
/// the trigger" shape this codebase already uses everywhere (<c>TaxRule</c>
/// not wired into <c>AddLine</c>, price lists not resolved through it,
/// DAT-07's role with no real consumer yet). Comparing the count against
/// an expected total — reconciling variance at the end of a shift — is
/// PAY-11's own later task, not this.
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
/// <para>
/// <b>A blind cash count (PAY-10) is at most once per session.</b> Staff
/// count what's physically in the drawer and record it via
/// <see cref="RecordCount"/> — "blind" because nothing in this codebase
/// shows them an expected total to compare against first (no variance
/// calculation exists yet; that's PAY-11's own later task, which will need
/// this count as one of its inputs). A second count on the same session is
/// rejected (<c>cash_session.already_counted</c>) rather than silently
/// overwriting the first — the entire value of a blind count is that it
/// wasn't influenced by anything, including an earlier attempt.
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

    /// <summary>Closes the session. A bare status flip — variance reporting is PAY-11, not this.</summary>
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

    /// <summary>How much cash was physically counted in the drawer, if it has been (PAY-10).</summary>
    public Money? CountedAmount { get; private set; }

    /// <summary>Which staff member counted it, if it has been.</summary>
    public Guid? CountedByStaffId { get; private set; }

    /// <summary>When the count was recorded, if it has been.</summary>
    public DateTimeOffset? CountedAtUtc { get; private set; }

    /// <summary>
    /// Records a blind cash count against this session — at most once, and
    /// only while the session is still open (counting happens as part of
    /// closing out a shift, not afterward). No comparison against an
    /// expected total is computed here — see the class remarks.
    /// </summary>
    /// <param name="staffId">Who counted. Never validated here — see the class remarks.</param>
    /// <param name="countedAmount">What was physically counted. Must not be negative — an empty drawer is zero, not invalid.</param>
    /// <param name="countedAtUtc">When the count was recorded.</param>
    public Result RecordCount(Guid staffId, Money countedAmount, DateTimeOffset countedAtUtc)
    {
        if (countedAmount.IsNegative)
        {
            return Result.Failure(Error.Validation(
                "cash_session.invalid_counted_amount", "Counted amount must not be negative."));
        }

        if (!IsOpen)
        {
            return Result.Failure(Error.Validation(
                "cash_session.already_closed", "This cash session is already closed."));
        }

        if (CountedAmount is not null)
        {
            return Result.Failure(Error.Validation(
                "cash_session.already_counted", "This cash session has already been counted."));
        }

        CountedByStaffId = staffId;
        CountedAmount = countedAmount;
        CountedAtUtc = countedAtUtc;
        return Result.Success();
    }
}
