using Brasa.Modules.Payments.Domain;

namespace Brasa.Api.Contracts;

/// <summary>
/// A cash session — <i>abertura de caixa</i> (PAY-08) — recorded against a
/// terminal. <c>TerminalLabel</c>/<c>OpenedByStaffName</c> are resolved
/// fresh from Identity, the same pattern <c>PaymentDto.AttributedStaffName</c>
/// already uses, never snapshotted onto the session itself.
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
    bool IsOpen);

/// <summary>
/// Request body to open a cash session. Rejected if <c>TerminalId</c>
/// already has an open session (<c>cash_session.already_open</c>) — only
/// one at a time per terminal.
/// </summary>
public sealed record OpenCashSessionRequest(Guid TerminalId, Guid StaffId, decimal OpeningFloat);

/// <summary>Maps <see cref="CashSession"/> to its wire DTO.</summary>
public static class CashSessionDtoMappings
{
    public static CashSessionDto ToDto(this CashSession session, string terminalLabel, string openedByStaffName) => new(
        session.Id,
        session.TerminalId,
        terminalLabel,
        session.OpenedByStaffId,
        openedByStaffName,
        session.OpeningFloat.ToDto(),
        session.OpenedAtUtc.ToString("O"),
        session.ClosedAtUtc?.ToString("O"),
        session.IsOpen);
}
