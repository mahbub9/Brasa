using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// A tender recorded against an order — PAY-01's model, PAY-02's cash case,
/// PAY-05's partial-payment support.
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
    /// <param name="paidAtUtc">When the payment was recorded.</param>
    public Payment(Guid orderId, PaymentMethod method, Money amountDue, Money amountTendered, DateTimeOffset paidAtUtc)
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

        OrderId = orderId;
        Method = method;
        AmountDue = amountDue;
        AmountTendered = amountTendered;
        PaidAtUtc = paidAtUtc;
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

    /// <summary>When this payment was recorded.</summary>
    public DateTimeOffset PaidAtUtc { get; private set; }
}
