using Brasa.Modules.Payments.Domain;

namespace Brasa.Api.Contracts;

/// <summary>
/// A cash session — <i>abertura de caixa</i> (PAY-08) — recorded against a
/// terminal. <c>TerminalLabel</c>/<c>OpenedByStaffName</c>/
/// <c>CountedByStaffName</c> are resolved fresh from Identity, the same
/// pattern <c>PaymentDto.AttributedStaffName</c> already uses, never
/// snapshotted onto the session itself. The <c>Counted*</c> fields are all
/// <c>null</c> together until a blind count is recorded (PAY-10) — see
/// <see cref="CashSession.RecordCount"/>.
/// </summary>
public sealed record CashSessionDto(
    Guid Id,
    Guid TerminalId,
    string TerminalLabel,
    Guid OpenedByStaffId,
    string OpenedByStaffName,
    MoneyDto OpeningFloat,
    string OpenedAtUtc,
    string? ClosedAtUtc,
    bool IsOpen,
    MoneyDto? CountedAmount,
    Guid? CountedByStaffId,
    string? CountedByStaffName,
    string? CountedAtUtc);

/// <summary>
/// Request body to open a cash session. Rejected if <c>TerminalId</c>
/// already has an open session (<c>cash_session.already_open</c>) — only
/// one at a time per terminal.
/// </summary>
public sealed record OpenCashSessionRequest(Guid TerminalId, Guid StaffId, decimal OpeningFloat);

/// <summary>
/// Request body to record a blind cash count (PAY-10) — see
/// <see cref="CashSession.RecordCount"/> for why "blind" and why at most
/// once per session.
/// </summary>
public sealed record RecordCashCountRequest(Guid StaffId, decimal CountedAmount);

/// <summary>Maps <see cref="CashSession"/> to its wire DTO.</summary>
public static class CashSessionDtoMappings
{
    /// <param name="countedByStaffName">Resolved by the caller when <see cref="CashSession.CountedByStaffId"/> is set; omitted (stays <c>null</c>) otherwise.</param>
    public static CashSessionDto ToDto(
        this CashSession session, string terminalLabel, string openedByStaffName, string? countedByStaffName = null) => new(
        session.Id,
        session.TerminalId,
        terminalLabel,
        session.OpenedByStaffId,
        openedByStaffName,
        session.OpeningFloat.ToDto(),
        session.OpenedAtUtc.ToString("O"),
        session.ClosedAtUtc?.ToString("O"),
        session.IsOpen,
        session.CountedAmount?.ToDto(),
        session.CountedByStaffId,
        countedByStaffName,
        session.CountedAtUtc?.ToString("O"));
}
