using Brasa.Api.Contracts;
using Brasa.Modules.Identity.Persistence;
using Brasa.Modules.Payments.Domain;
using Brasa.Modules.Payments.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Cash session endpoints (PAY-08) — <i>abertura de caixa</i>, a staff
/// member declaring a starting float against a terminal at the start of a
/// shift. Composes <see cref="PaymentsDbContext"/> (this module's own
/// table) and <see cref="IdentityDbContext"/> (to confirm a real terminal
/// and staff member, and resolve their names) at the API layer, the same
/// shape <see cref="PaymentEndpoints"/> already uses for tip attribution.
/// </summary>
/// <remarks>
/// <b>Purely a record of the declaration — does not gate anything else
/// yet.</b> See <see cref="CashSession"/>'s own class remarks. Counting the
/// float back out and reconciling variance is PAY-10/11's own later task.
/// </remarks>
public static class CashSessionEndpoints
{
    /// <summary>Maps the cash session endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapCashSessionEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/cash-sessions", OpenCashSessionAsync)
            .WithName("OpenCashSession")
            .WithSummary("Opens a cash session against a terminal, declaring a starting float (PAY-08). Rejected if that terminal already has an open session — only one at a time.")
            .Produces<CashSessionDto>(StatusCodes.Status201Created);

        group.MapPost("/cash-sessions/{cashSessionId:guid}/close", CloseCashSessionAsync)
            .WithName("CloseCashSession")
            .WithSummary("Closes a cash session. A bare status flip today — variance reporting against a blind count is PAY-10/11, not this.")
            .Produces<CashSessionDto>();

        group.MapGet("/cash-sessions/{cashSessionId:guid}", GetCashSessionAsync)
            .WithName("GetCashSession")
            .WithSummary("Fetches one cash session by id.")
            .Produces<CashSessionDto>();

        group.MapGet("/terminals/{terminalId:guid}/cash-sessions/current", GetCurrentCashSessionAsync)
            .WithName("GetCurrentCashSession")
            .WithSummary("Returns the currently open cash session for a terminal (200), or 204 with no body if none is open.")
            .Produces<CashSessionDto>()
            .Produces(StatusCodes.Status204NoContent);

        return group;
    }

    private static async Task<IResult> OpenCashSessionAsync(
        OpenCashSessionRequest request,
        PaymentsDbContext paymentsDb,
        IdentityDbContext identityDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var terminal = await identityDb.Terminals
            .FirstOrDefaultAsync(t => t.Id == request.TerminalId, cancellationToken)
            .ConfigureAwait(false);

        if (terminal is null)
        {
            return Error.NotFound(
                "identity.terminal_not_found", $"Terminal {request.TerminalId} was not found.").ToProblem();
        }

        var staff = await identityDb.Staff
            .FirstOrDefaultAsync(s => s.Id == request.StaffId, cancellationToken)
            .ConfigureAwait(false);

        if (staff is null)
        {
            return Error.NotFound("identity.staff_not_found", $"Staff member {request.StaffId} was not found.").ToProblem();
        }

        if (request.OpeningFloat < 0)
        {
            return Error.Validation(
                "cash_session.invalid_opening_float", "Opening float must not be negative.").ToProblem();
        }

        // Only one open session per terminal at a time — see CashSession's
        // own class remarks for why this is enforced here, not in the
        // domain type itself (it needs to see every other session for the
        // same terminal, which is a query, not an invariant of one row).
        var alreadyOpen = await paymentsDb.CashSessions
            .AnyAsync(c => c.TerminalId == request.TerminalId && c.ClosedAtUtc == null, cancellationToken)
            .ConfigureAwait(false);

        if (alreadyOpen)
        {
            return Error.Conflict(
                "cash_session.already_open",
                $"Terminal {request.TerminalId} already has an open cash session.").ToProblem();
        }

        var session = new CashSession(
            request.TerminalId, request.StaffId, Money.FromDecimal(request.OpeningFloat), clock.UtcNow);
        paymentsDb.CashSessions.Add(session);
        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/cash-sessions/{session.Id}", session.ToDto(terminal.Label, staff.Name));
    }

    private static async Task<IResult> CloseCashSessionAsync(
        Guid cashSessionId,
        PaymentsDbContext paymentsDb,
        IdentityDbContext identityDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var session = await paymentsDb.CashSessions
            .FirstOrDefaultAsync(c => c.Id == cashSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return Error.NotFound("cash_session.not_found", $"Cash session {cashSessionId} was not found.").ToProblem();
        }

        var closeResult = session.Close(clock.UtcNow);
        if (closeResult.IsFailure)
        {
            return closeResult.Error.ToProblem();
        }

        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (terminalLabel, staffName) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, staffName));
    }

    private static async Task<IResult> GetCashSessionAsync(
        Guid cashSessionId,
        PaymentsDbContext paymentsDb,
        IdentityDbContext identityDb,
        CancellationToken cancellationToken)
    {
        var session = await paymentsDb.CashSessions
            .FirstOrDefaultAsync(c => c.Id == cashSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return Error.NotFound("cash_session.not_found", $"Cash session {cashSessionId} was not found.").ToProblem();
        }

        var (terminalLabel, staffName) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, staffName));
    }

    private static async Task<IResult> GetCurrentCashSessionAsync(
        Guid terminalId,
        PaymentsDbContext paymentsDb,
        IdentityDbContext identityDb,
        CancellationToken cancellationToken)
    {
        var terminalExists = await identityDb.Terminals
            .AnyAsync(t => t.Id == terminalId, cancellationToken)
            .ConfigureAwait(false);

        if (!terminalExists)
        {
            return Error.NotFound("identity.terminal_not_found", $"Terminal {terminalId} was not found.").ToProblem();
        }

        var session = await paymentsDb.CashSessions
            .Where(c => c.TerminalId == terminalId && c.ClosedAtUtc == null)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            // Not an error — "no open session" is a normal, common state
            // for a terminal between shifts, not a 404. 204 rather than
            // "200 with a null body": Results.Ok<T>(null) writes zero bytes
            // for the body, not the JSON literal "null", so a client
            // calling response.json() unconditionally would fail to parse
            // it — a real trap found live via the E2E suite, not assumed.
            return Results.NoContent();
        }

        var (terminalLabel, staffName) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, staffName));
    }

    private static async Task<(string TerminalLabel, string StaffName)> ResolveNamesAsync(
        CashSession session, IdentityDbContext identityDb, CancellationToken cancellationToken)
    {
        var terminal = await identityDb.Terminals
            .FirstOrDefaultAsync(t => t.Id == session.TerminalId, cancellationToken)
            .ConfigureAwait(false);
        var staff = await identityDb.Staff
            .FirstOrDefaultAsync(s => s.Id == session.OpenedByStaffId, cancellationToken)
            .ConfigureAwait(false);

        // Both were confirmed real at Open() time and neither Terminal nor
        // Staff has a delete path yet, so a missing row here would be a
        // genuine bug, not an expected runtime state — the fallback below
        // only guards a hypothetical rather than adding a new error path
        // for something that can't happen today.
        return (terminal?.Label ?? "?", staff?.Name ?? "?");
    }
}
