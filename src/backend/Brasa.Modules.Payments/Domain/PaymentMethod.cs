namespace Brasa.Modules.Payments.Domain;

/// <summary>
/// How a payment was tendered. <see cref="Card"/> (PAY-03) is a manually
/// captured tender from a standalone TPA — this codebase never talks to a
/// card processor, staff key the already-charged amount in after the real
/// terminal approves it, the same "record what happened, don't drive the
/// hardware" shape <see cref="Cash"/> already has. Split tender and meal
/// vouchers (PAY-04/12) are each their own later task, not stubbed here
/// ahead of need.
/// </summary>
public enum PaymentMethod
{
    Cash = 0,
    Card = 1,
}
