using Brasa.Modules.Payments.Domain;

namespace Brasa.Api.Contracts;

/// <summary>
/// A pay-in or pay-out against an open cash session (PAY-09).
/// <c>RecordedByStaffName</c> is resolved fresh from Identity, the same
/// pattern <see cref="PaymentDto.AttributedStaffName"/> already uses.
/// </summary>
public sealed record CashMovementDto(
    Guid Id,
    Guid CashSessionId,
    string Direction,
    MoneyDto Amount,
    string Reason,
    Guid RecordedByStaffId,
    string RecordedByStaffName,
    string RecordedAtUtc);

/// <summary>
/// Request body to record a cash movement. <c>Direction</c> is
/// <c>"PayIn"</c> or <c>"PayOut"</c>; <c>Amount</c> is always positive —
/// direction carries the sign. Rejected if the named session isn't
/// currently open (<c>cash_movement.session_closed</c>).
/// </summary>
public sealed record RecordCashMovementRequest(string Direction, decimal Amount, string Reason, Guid StaffId);

/// <summary>Maps <see cref="CashMovement"/> to its wire DTO.</summary>
public static class CashMovementDtoMappings
{
    public static CashMovementDto ToDto(this CashMovement movement, string recordedByStaffName) => new(
        movement.Id,
        movement.CashSessionId,
        movement.Direction.ToString(),
        movement.Amount.ToDto(),
        movement.Reason,
        movement.RecordedByStaffId,
        recordedByStaffName,
        movement.RecordedAtUtc.ToString("O"));
}
