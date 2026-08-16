using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// A tender recorded against an order — PAY-01's model, PAY-02's cash case.
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
/// <b>Full payment only — no partial tender (PAY-05).</b> <see cref="AmountTendered"/>
/// must cover <see cref="AmountDue"/> in one record; splitting a bill across
/// several payments or several methods is PAY-04/05, not this.
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
    /// <param name="amountDue">The order's own total at the moment of payment, snapshotted — never re-read live later.</param>
    /// <param name="amountTendered">What the guest actually handed over. Must cover <paramref name="amountDue"/> — see <see cref="Change"/>.</param>
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

        if (amountTendered < amountDue)
        {
            throw new ArgumentException(
                "Amount tendered must cover the amount due — partial payment is not supported yet (PAY-05).",
                nameof(amountTendered));
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

    /// <summary>The order's own total at the moment of payment.</summary>
    public Money AmountDue { get; private set; }

    /// <summary>What the guest actually handed over.</summary>
    public Money AmountTendered { get; private set; }

    /// <summary>What's owed back — always zero or positive by construction.</summary>
    public Money Change => AmountTendered - AmountDue;

    /// <summary>When this payment was recorded.</summary>
    public DateTimeOffset PaidAtUtc { get; private set; }
}
