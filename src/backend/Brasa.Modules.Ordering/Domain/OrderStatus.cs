namespace Brasa.Modules.Ordering.Domain;

/// <summary>Lifecycle state of an <see cref="Order"/>.</summary>
/// <remarks>
/// I0 started with exactly two states; <see cref="Merged"/> was added for
/// ORD-14 without a migration — <c>OrderConfiguration</c> stores this as a
/// string column, so a new enum member is schema-compatible. Course firing
/// and per-line tracking (epic ORD, I2) may add further states later; this
/// is still deliberately close to the smallest state machine that supports
/// "take an order, then pay for it".
/// </remarks>
public enum OrderStatus
{
    /// <summary>Accepting lines and payment.</summary>
    Open = 0,

    /// <summary>Paid and finalised. No further lines or state changes.</summary>
    Closed = 1,

    /// <summary>
    /// Merged into another order (ORD-14) — every line moved elsewhere via
    /// <c>DetachLine</c>, so nothing about this order was billed or
    /// fiscally issued. Distinct from <see cref="Closed"/>, which implies a
    /// fiscal document exists for it.
    /// </summary>
    Merged = 2,
}
