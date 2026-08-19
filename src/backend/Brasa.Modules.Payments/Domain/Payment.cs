using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// A tender recorded against an order — PAY-01's model, PAY-02's cash case,
/// PAY-03's card case, PAY-05's partial-payment support, PAY-06's tip
/// recording and attribution.
/// </summary>
/// <remarks>
/// <para>
/// <b>Purely additive — does not gate <c>Order.Close()</c>.</b> Real service
/// (I2, already shipped) and this record both close over the same "guest
/// paid" moment, but wiring a payment requirement into the already-proven
/// close path would touch the dozens of existing E2E specs that close an
/// order without ever recording one — the same "mechanism before the
/// trigger" shape this codebase already uses everywhere (<c>TaxRule</c> not
/// wired into <c>AddLine</c>, price lists not resolved through it, DAT-07's
/// role with no real consumer yet). This ships the record; requiring one
/// before close is deliberately a separate, later decision.
/// </para>
/// <para>
/// <b><see cref="AmountDue"/> is a remaining balance, not always the order's
/// full total (PAY-05).</b> The API layer passes in whatever is still owed
/// at the moment this payment is recorded — the order's own total minus
/// every prior payment's <see cref="AmountApplied"/> — so a guest can settle
/// an order across several tenders. <see cref="AmountTendered"/> no longer
/// has to cover <see cref="AmountDue"/> in one record: tendering less than
/// what's owed is a valid partial payment (<see cref="AmountApplied"/> equals
/// the tender, <see cref="RemainingBalance"/> stays positive); tendering more
/// settles the balance and returns <see cref="Change"/>. Splitting a single
/// payment across several *methods* at once (e.g. half cash, half card) is
/// still PAY-04, not this — every <c>Payment</c> row here is still exactly
/// one method.
/// </para>
/// <para>
/// <c>OrderId</c> is a plain opaque reference, the same convention
/// <c>Order.TableId</c> already uses for a Floor <c>Table</c> — Payments
/// never queries Ordering directly (see
/// <c>docs/architecture/module-boundaries.md</c>); the API layer confirms
/// the order is real and reads its current total before this is ever
/// constructed.
/// </para>
/// <para>
/// <b>A tip is separate from the bill (PAY-06).</b> <see cref="TipAmount"/>
/// never affects <see cref="AmountDue"/>/<see cref="AmountApplied"/>/
/// <see cref="RemainingBalance"/> — it is extra money on top, recorded
/// alongside this same tender rather than as its own row, the same "one
/// tender, one <c>Payment</c> row" shape the rest of this type already has.
/// <see cref="AttributedStaffId"/> is optional even when a tip is given (an
/// unattributed tip goes to a shared pool) and, like <c>OrderId</c>, is a
/// plain opaque reference — Payments never queries Identity directly; the
/// API layer confirms a given staff id is real before this is constructed.
/// </para>
/// <para>
/// <b>A card tender cannot overpay (PAY-03).</b> A standalone TPA charges
/// exactly the amount keyed in — there is no "change" mechanism a card
/// terminal can hand back the way a cash drawer can. Tendering less than
/// <paramref name="amountDue"/> by card is still a valid partial payment,
/// the same as cash (PAY-05); tendering more is rejected rather than
/// silently producing a <see cref="Change"/> value that could never
/// actually be paid out.
/// </para>
/// <para>
/// <b><see cref="CashSessionId"/> is optional, unvalidated by this type
/// (PAY-11).</b> A plain opaque reference to a <c>CashSession</c> in this
/// same module, the same convention <c>CashMovement.CashSessionId</c>
/// already uses — the API layer confirms it names a real session before
/// this is constructed, if one is given at all. Lets a variance report
/// (PAY-11) later ask "how much cash was taken during this session," an
/// input the report literally cannot answer without it. No client sends
/// one today — no UI anywhere has a "current cash session" concept yet, the
/// same "mechanism before the trigger" shape this codebase already uses
/// everywhere.
/// </para>
/// </remarks>
public sealed class Payment : Entity
{
    private Payment()
    {
        // EF Core materialisation.
    }

    /// <summary>Records a tender against an order.</summary>
    /// <param name="orderId">The order this payment settles. Never validated here — see the class remarks.</param>
    /// <param name="method">How it was tendered.</param>
    /// <param name="amountDue">What was still owed on the order at the moment of payment — see the class remarks. Snapshotted, never re-read live later.</param>
    /// <param name="amountTendered">What the guest actually handed over. Need not cover <paramref name="amountDue"/> — see <see cref="AmountApplied"/> and <see cref="RemainingBalance"/>.</param>
    /// <param name="tipAmount">Extra money on top of the bill, in the same currency as <paramref name="amountDue"/>. Zero when there is no tip.</param>
    /// <param name="attributedStaffId">Which staff member the tip is credited to, if any. Never validated here — see the class remarks.</param>
    /// <param name="paidAtUtc">When the payment was recorded.</param>
    /// <param name="cashSessionId">Which cash session this tender was taken under, if any. Never validated here — see the class remarks.</param>
    public Payment(
        Guid orderId,
        PaymentMethod method,
        Money amountDue,
        Money amountTendered,
        Money tipAmount,
        Guid? attributedStaffId,
        DateTimeOffset paidAtUtc,
        Guid? cashSessionId = null)
    {
        if (orderId == Guid.Empty)
        {
            throw new ArgumentException("Order id must not be empty.", nameof(orderId));
        }

        if (!amountDue.IsPositive)
        {
            throw new ArgumentException("Amount due must be positive.", nameof(amountDue));
        }

        if (!amountTendered.IsPositive)
        {
            throw new ArgumentException("Amount tendered must be positive.", nameof(amountTendered));
        }

        if (method == PaymentMethod.Card && amountTendered > amountDue)
        {
            throw new ArgumentException(
                "A card tender cannot exceed what's still owed — a card has no change to give back.",
                nameof(amountTendered));
        }

        if (tipAmount.IsNegative)
        {
            throw new ArgumentException("Tip amount must not be negative.", nameof(tipAmount));
        }

        if (attributedStaffId is not null && !tipAmount.IsPositive)
        {
            throw new ArgumentException(
                "A tip can only be attributed to a staff member when the tip amount is positive.",
                nameof(attributedStaffId));
        }

        OrderId = orderId;
        Method = method;
        AmountDue = amountDue;
        AmountTendered = amountTendered;
        TipAmount = tipAmount;
        AttributedStaffId = attributedStaffId;
        PaidAtUtc = paidAtUtc;
        CashSessionId = cashSessionId;
    }

    /// <summary>The order this payment settles.</summary>
    public Guid OrderId { get; private set; }

    /// <summary>How this was tendered.</summary>
    public PaymentMethod Method { get; private set; }

    /// <summary>What was still owed on the order at the moment of this payment.</summary>
    public Money AmountDue { get; private set; }

    /// <summary>What the guest actually handed over.</summary>
    public Money AmountTendered { get; private set; }

    /// <summary>
    /// How much of <see cref="AmountTendered"/> actually reduces the order's
    /// balance — the smaller of what was handed over and what was still
    /// owed. Equal to <see cref="AmountTendered"/> for a partial or exact
    /// tender; capped at <see cref="AmountDue"/> for a tender that overpays.
    /// </summary>
    public Money AmountApplied => AmountTendered <= AmountDue ? AmountTendered : AmountDue;

    /// <summary>What's owed back to the guest — positive only when this tender overpaid.</summary>
    public Money Change => AmountTendered - AmountApplied;

    /// <summary>What's still owed on the order after this payment — zero once fully settled.</summary>
    public Money RemainingBalance => AmountDue - AmountApplied;

    /// <summary>Extra money on top of the bill — zero when there is no tip. Never affects the balance above.</summary>
    public Money TipAmount { get; private set; }

    /// <summary>Which staff member <see cref="TipAmount"/> is credited to, if any. Always <c>null</c> when there is no tip.</summary>
    public Guid? AttributedStaffId { get; private set; }

    /// <summary>When this payment was recorded.</summary>
    public DateTimeOffset PaidAtUtc { get; private set; }

    /// <summary>Which cash session this tender was taken under, if any (PAY-11).</summary>
    public Guid? CashSessionId { get; private set; }
}
