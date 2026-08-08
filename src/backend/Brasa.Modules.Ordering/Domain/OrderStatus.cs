namespace Brasa.Modules.Ordering.Domain;

/// <summary>Lifecycle state of an <see cref="Order"/>.</summary>
/// <remarks>
/// I0 has exactly two states. Course firing, kitchen status, and per-line
/// tracking (epic ORD, I2) add states later; this is deliberately the smallest
/// state machine that supports "take an order, then pay for it".
/// </remarks>
public enum OrderStatus
{
    /// <summary>Accepting lines and payment.</summary>
    Open = 0,

    /// <summary>Paid and finalised. No further lines or state changes.</summary>
    Closed = 1,
}
