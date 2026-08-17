using Brasa.Modules.Payments.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A tender recorded against an order (PAY-01/02/03/05/06).</summary>
/// <remarks>
/// <c>AmountDue</c> is what was still owed at the moment of this payment, not
/// always the order's full total — see <see cref="Payment"/>'s own remarks.
/// <c>AmountApplied</c> is how much of <c>AmountTendered</c> actually reduced
/// the balance; <c>RemainingBalance</c> is what's still owed afterward, zero
/// once the order is fully settled (PAY-05). <c>TipAmount</c> is separate
/// from all of the above — extra money on top of the bill, never affecting
/// the balance (PAY-06); <c>AttributedStaffName</c> is resolved fresh from
/// Identity, the same pattern <c>RoomDto.AssignedStaffName</c> already uses,
/// never snapshotted onto the payment itself.
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
    MoneyDto TipAmount,
    Guid? AttributedStaffId,
    string? AttributedStaffName,
    string PaidAtUtc);

/// <summary>
/// Request body to record a payment. <c>AmountTendered</c> only for the bill
/// itself — the amount due is never client-supplied, it is computed from the
/// order's own remaining balance server-side, the same "never trust a
/// client-sent total" instinct every other money-handling endpoint in this
/// codebase already follows. <c>Method</c> is <c>"Cash"</c> or <c>"Card"</c>
/// (PAY-03) — a card tender cannot exceed the balance, since a standalone TPA
/// has no change to give back. <c>TipAmount</c> defaults to zero when
/// omitted; <c>StaffId</c> is optional even when a tip is given — an
/// unattributed tip goes to a shared pool (PAY-06).
/// </summary>
public sealed record RecordPaymentRequest(string Method, decimal AmountTendered, decimal TipAmount = 0, Guid? StaffId = null);

/// <summary>Maps <see cref="Payment"/> to its wire DTO.</summary>
public static class PaymentDtoMappings
{
    /// <summary>
    /// Converts a payment to its wire representation. <paramref name="attributedStaffName"/>
    /// is resolved by the caller (a batched Identity lookup when listing
    /// several payments at once, a single lookup when recording one) rather
    /// than queried here, the same shape <c>RoomDto.ToDto</c> already uses.
    /// </summary>
    public static PaymentDto ToDto(this Payment payment, string? attributedStaffName = null) => new(
        payment.Id,
        payment.OrderId,
        payment.Method.ToString(),
        payment.AmountDue.ToDto(),
        payment.AmountTendered.ToDto(),
        payment.AmountApplied.ToDto(),
        payment.Change.ToDto(),
        payment.RemainingBalance.ToDto(),
        payment.TipAmount.ToDto(),
        payment.AttributedStaffId,
        attributedStaffName,
        payment.PaidAtUtc.ToString("O"));
}
