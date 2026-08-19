using Brasa.Api.Contracts;
using Brasa.Modules.Identity.Persistence;
using Brasa.Modules.Payments.Domain;
using Brasa.Modules.Payments.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Cash session endpoints (PAY-08/09/10/11) — <i>abertura de caixa</i>, a
/// staff member declaring a starting float against a terminal at the start
/// of a shift, pay-in/pay-out movements against an open session, a blind
/// count of what's in the drawer, and a computed variance report comparing
/// the two. Composes <see cref="PaymentsDbContext"/> (this module's own
/// tables — <c>Payment</c> lives here too, so the variance report needs no
/// new cross-module composition at all) and <see cref="IdentityDbContext"/>
/// (to confirm a real terminal and staff member, and resolve their names)
/// at the API layer, the same shape <see cref="PaymentEndpoints"/> already
/// uses for tip attribution.
/// </summary>
/// <remarks>
/// <b>Purely a record — does not gate anything else yet.</b> See
/// <see cref="CashSession"/>'s own class remarks. The variance report
/// (PAY-11) only ever reads; nothing about closing a session requires one
/// to have been computed, or requires the numbers to reconcile.
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
            .WithSummary("Closes a cash session. A bare status flip today — variance reporting against a blind count is PAY-11, not this.")
            .Produces<CashSessionDto>();

        group.MapPost("/cash-sessions/{cashSessionId:guid}/count", RecordCashCountAsync)
            .WithName("RecordCashCount")
            .WithSummary("Records a blind cash count against an open session, at most once (PAY-10). No comparison against an expected total — variance reporting is PAY-11, not this.")
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

        group.MapPost("/cash-sessions/{cashSessionId:guid}/movements", RecordCashMovementAsync)
            .WithName("RecordCashMovement")
            .WithSummary("Records a pay-in or pay-out against an open cash session, with a required reason (PAY-09). Rejected if the session is already closed.")
            .Produces<CashMovementDto>(StatusCodes.Status201Created);

        group.MapGet("/cash-sessions/{cashSessionId:guid}/movements", GetCashMovementsAsync)
            .WithName("GetCashMovements")
            .WithSummary("Lists every pay-in/pay-out recorded against a cash session, oldest first.")
            .Produces<List<CashMovementDto>>();

        group.MapGet("/cash-sessions/{cashSessionId:guid}/variance", GetCashSessionVarianceAsync)
            .WithName("GetCashSessionVariance")
            .WithSummary("Computes a fecho de caixa variance report (PAY-11) — expected drawer contents (float + pay-ins - pay-outs + cash payments taken) versus the blind count, if one has been recorded. Never stored; recomputed on every call.")
            .Produces<CashSessionVarianceDto>();

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

        var (terminalLabel, staffName, countedByStaffName) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, staffName, countedByStaffName));
    }

    private static async Task<IResult> RecordCashCountAsync(
        Guid cashSessionId,
        RecordCashCountRequest request,
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

        var staff = await identityDb.Staff
            .FirstOrDefaultAsync(s => s.Id == request.StaffId, cancellationToken)
            .ConfigureAwait(false);

        if (staff is null)
        {
            return Error.NotFound("identity.staff_not_found", $"Staff member {request.StaffId} was not found.").ToProblem();
        }

        var countResult = session.RecordCount(request.StaffId, Money.FromDecimal(request.CountedAmount), clock.UtcNow);
        if (countResult.IsFailure)
        {
            return countResult.Error.ToProblem();
        }

        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var (terminalLabel, openedByStaffName, _) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, openedByStaffName, staff.Name));
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

        var (terminalLabel, staffName, countedByStaffName) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, staffName, countedByStaffName));
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

        var (terminalLabel, staffName, countedByStaffName) = await ResolveNamesAsync(session, identityDb, cancellationToken).ConfigureAwait(false);
        return Results.Ok(session.ToDto(terminalLabel, staffName, countedByStaffName));
    }

    private static async Task<(string TerminalLabel, string StaffName, string? CountedByStaffName)> ResolveNamesAsync(
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
        string? countedByStaffName = null;
        if (session.CountedByStaffId is { } countedByStaffId)
        {
            var countedByStaff = await identityDb.Staff
                .FirstOrDefaultAsync(s => s.Id == countedByStaffId, cancellationToken)
                .ConfigureAwait(false);
            countedByStaffName = countedByStaff?.Name ?? "?";
        }

        return (terminal?.Label ?? "?", staff?.Name ?? "?", countedByStaffName);
    }

    private static async Task<IResult> RecordCashMovementAsync(
        Guid cashSessionId,
        RecordCashMovementRequest request,
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

        if (!session.IsOpen)
        {
            return Error.Validation(
                "cash_movement.session_closed",
                $"Cash session {cashSessionId} is closed — a movement can only be recorded against an open session.").ToProblem();
        }

        if (!Enum.TryParse<CashMovementDirection>(request.Direction, ignoreCase: true, out var direction))
        {
            return Error.Validation(
                "cash_movement.invalid_direction", $"\"{request.Direction}\" is not \"PayIn\" or \"PayOut\".").ToProblem();
        }

        if (request.Amount <= 0)
        {
            return Error.Validation(
                "cash_movement.invalid_amount", "Amount must be greater than zero.").ToProblem();
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return Error.Validation("cash_movement.reason_required", "A reason is required.").ToProblem();
        }

        var staff = await identityDb.Staff
            .FirstOrDefaultAsync(s => s.Id == request.StaffId, cancellationToken)
            .ConfigureAwait(false);

        if (staff is null)
        {
            return Error.NotFound("identity.staff_not_found", $"Staff member {request.StaffId} was not found.").ToProblem();
        }

        var movement = new CashMovement(
            cashSessionId, direction, Money.FromDecimal(request.Amount), request.Reason, request.StaffId, clock.UtcNow);
        paymentsDb.CashMovements.Add(movement);
        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/cash-sessions/{cashSessionId}/movements/{movement.Id}", movement.ToDto(staff.Name));
    }

    private static async Task<IResult> GetCashMovementsAsync(
        Guid cashSessionId,
        PaymentsDbContext paymentsDb,
        IdentityDbContext identityDb,
        CancellationToken cancellationToken)
    {
        var sessionExists = await paymentsDb.CashSessions
            .AnyAsync(c => c.Id == cashSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (!sessionExists)
        {
            return Error.NotFound("cash_session.not_found", $"Cash session {cashSessionId} was not found.").ToProblem();
        }

        var movements = await paymentsDb.CashMovements
            .Where(m => m.CashSessionId == cashSessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // One batched Identity query for every distinct RecordedByStaffId,
        // not N+1 — the same shape PaymentEndpoints.GetPaymentsAsync
        // already uses for AttributedStaffName.
        var distinctStaffIds = movements.Select(m => m.RecordedByStaffId).Distinct().ToList();
        var staffNamesById = distinctStaffIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityDb.Staff
                .Where(s => distinctStaffIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken)
                .ConfigureAwait(false);

        // Sorted client-side: SQLite's EF Core provider (ADR 0012) cannot
        // translate an ORDER BY over DateTimeOffset — the same limitation
        // TaxRuleEndpoints.GetTaxRulesAsync already works around.
        var ordered = movements
            .OrderBy(m => m.RecordedAtUtc)
            .Select(m => m.ToDto(staffNamesById.GetValueOrDefault(m.RecordedByStaffId, "?")))
            .ToList();

        return Results.Ok(ordered);
    }

    private static async Task<IResult> GetCashSessionVarianceAsync(
        Guid cashSessionId,
        PaymentsDbContext paymentsDb,
        CancellationToken cancellationToken)
    {
        var session = await paymentsDb.CashSessions
            .FirstOrDefaultAsync(c => c.Id == cashSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
        {
            return Error.NotFound("cash_session.not_found", $"Cash session {cashSessionId} was not found.").ToProblem();
        }

        var movements = await paymentsDb.CashMovements
            .Where(m => m.CashSessionId == cashSessionId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var totalPayIns = Money.Sum(movements.Where(m => m.Direction == CashMovementDirection.PayIn).Select(m => m.Amount));
        var totalPayOuts = Money.Sum(movements.Where(m => m.Direction == CashMovementDirection.PayOut).Select(m => m.Amount));

        // Only Cash payments count toward the physical drawer — a card
        // tender never touches it. Payment lives in this same
        // PaymentsDbContext (this module's own table), so no new
        // cross-module composition is needed to read it.
        var cashPayments = await paymentsDb.Payments
            .Where(p => p.CashSessionId == cashSessionId && p.Method == PaymentMethod.Cash)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var totalCashPaymentsTaken = Money.Sum(cashPayments.Select(p => p.AmountApplied));

        var expectedAmount = session.OpeningFloat + totalPayIns - totalPayOuts + totalCashPaymentsTaken;

        // Variance is only meaningful once a blind count exists (PAY-10) —
        // comparing against nothing would be a false "zero variance", not
        // an honest "not counted yet".
        Money? variance = session.CountedAmount is { } counted ? counted - expectedAmount : null;

        var dto = new CashSessionVarianceDto(
            session.Id,
            session.OpeningFloat.ToDto(),
            totalPayIns.ToDto(),
            totalPayOuts.ToDto(),
            totalCashPaymentsTaken.ToDto(),
            expectedAmount.ToDto(),
            session.CountedAmount?.ToDto(),
            variance?.ToDto());

        return Results.Ok(dto);
    }
}
