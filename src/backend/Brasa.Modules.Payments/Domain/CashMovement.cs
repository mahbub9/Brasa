using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Payments.Domain;

/// <summary>Which direction a <see cref="CashMovement"/> moves cash — into or out of the drawer.</summary>
public enum CashMovementDirection
{
    PayIn = 0,
    PayOut = 1,
}

/// <summary>
/// A pay-in or pay-out against an open <see cref="CashSession"/> (PAY-09) —
/// cash added to or removed from the drawer mid-shift for a reason other
/// than an order payment: change float top-up, petty cash for a delivery,
/// a bank drop. Always requires a reason, the same "never silent" instinct
/// <c>Order.VoidLine</c> already applies to voiding a line.
/// </summary>
/// <remarks>
/// <c>CashSessionId</c> is a plain opaque reference — validated as real,
/// and confirmed still open, by the API layer before this is ever
/// constructed, not by this type itself, the same "the check needs to see
/// state beyond one row" reasoning <see cref="CashSession"/>'s own
/// one-open-session-per-terminal rule already uses. <c>RecordedByStaffId</c>
/// is a plain opaque reference too, the same convention
/// <see cref="CashSession.OpenedByStaffId"/> already uses.
/// </remarks>
public sealed class CashMovement : Entity
{
    private CashMovement()
    {
        // EF Core materialisation.
    }

    /// <summary>Records a pay-in or pay-out against a cash session.</summary>
    /// <param name="cashSessionId">The session this movement belongs to. Never validated here — see the class remarks.</param>
    /// <param name="direction">Whether cash is added to or removed from the drawer.</param>
    /// <param name="amount">How much moved. Always positive — <paramref name="direction"/> carries the sign.</param>
    /// <param name="reason">Why. Required — a movement with no reason defeats the entire point of the record.</param>
    /// <param name="recordedByStaffId">Who recorded it. Never validated here — see the class remarks.</param>
    /// <param name="recordedAtUtc">When it was recorded.</param>
    public CashMovement(
        Guid cashSessionId,
        CashMovementDirection direction,
        Money amount,
        string reason,
        Guid recordedByStaffId,
        DateTimeOffset recordedAtUtc)
    {
        if (cashSessionId == Guid.Empty)
        {
            throw new ArgumentException("Cash session id must not be empty.", nameof(cashSessionId));
        }

        if (!amount.IsPositive)
        {
            throw new ArgumentException("Amount must be positive.", nameof(amount));
        }

        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("A reason is required.", nameof(reason));
        }

        if (recordedByStaffId == Guid.Empty)
        {
            throw new ArgumentException("Recorded-by staff id must not be empty.", nameof(recordedByStaffId));
        }

        CashSessionId = cashSessionId;
        Direction = direction;
        Amount = amount;
        Reason = reason.Trim();
        RecordedByStaffId = recordedByStaffId;
        RecordedAtUtc = recordedAtUtc;
    }

    /// <summary>The session this movement belongs to.</summary>
    public Guid CashSessionId { get; private set; }

    /// <summary>Whether cash was added to or removed from the drawer.</summary>
    public CashMovementDirection Direction { get; private set; }

    /// <summary>How much moved — always positive; <see cref="Direction"/> carries the sign.</summary>
    public Money Amount { get; private set; }

    /// <summary>Why the movement happened.</summary>
    public string Reason { get; private set; } = string.Empty;

    /// <summary>Which staff member recorded it.</summary>
    public Guid RecordedByStaffId { get; private set; }

    /// <summary>When it was recorded.</summary>
    public DateTimeOffset RecordedAtUtc { get; private set; }
}
