namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// How a payment was tendered. Only <see cref="Cash"/> exists today (PAY-02)
/// — card, split tender, and meal vouchers (PAY-03/04/12) are each their own
/// later task, not stubbed here ahead of need.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
}
