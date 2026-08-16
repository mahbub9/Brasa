using Brasa.Modules.Payments.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A tender recorded against an order (PAY-01/02/05).</summary>
/// <remarks>
/// <c>AmountDue</c> is what was still owed at the moment of this payment, not
/// always the order's full total — see <see cref="Payment"/>'s own remarks.
/// <c>AmountApplied</c> is how much of <c>AmountTendered</c> actually reduced
/// the balance; <c>RemainingBalance</c> is what's still owed afterward, zero
/// once the order is fully settled (PAY-05).
/// </remarks>
public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    string Method,
    MoneyDto AmountDue,
    MoneyDto AmountTendered,
    MoneyDto AmountApplied,
    MoneyDto Change,
    MoneyDto RemainingBalance,
    string PaidAtUtc);

/// <summary>
/// Request body to record a payment. <c>AmountTendered</c> only — the amount
/// due is never client-supplied, it is read from the order's own current
/// total server-side, the same "never trust a client-sent total" instinct
/// every other money-handling endpoint in this codebase already follows.
/// </summary>
public sealed record RecordPaymentRequest(string Method, decimal AmountTendered);

/// <summary>Maps <see cref="Payment"/> to its wire DTO.</summary>
public static class PaymentDtoMappings
{
    public static PaymentDto ToDto(this Payment payment) => new(
        payment.Id,
        payment.OrderId,
        payment.Method.ToString(),
        payment.AmountDue.ToDto(),
        payment.AmountTendered.ToDto(),
        payment.AmountApplied.ToDto(),
        payment.Change.ToDto(),
        payment.RemainingBalance.ToDto(),
        payment.PaidAtUtc.ToString("O"));
}
