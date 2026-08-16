using Brasa.Modules.Payments.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A tender recorded against an order (PAY-01/02).</summary>
public sealed record PaymentDto(
    Guid Id,
    Guid OrderId,
    string Method,
    MoneyDto AmountDue,
    MoneyDto AmountTendered,
    MoneyDto Change,
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
        payment.Change.ToDto(),
        payment.PaidAtUtc.ToString("O"));
}
