using Brasa.Api.Contracts;
using Brasa.Modules.Identity.Persistence;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Modules.Payments.Domain;
using Brasa.Modules.Payments.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Tender endpoints (PAY-01/02/03/04/05/06/11). Composes <see cref="PaymentsDbContext"/>
/// (this module's own table), <see cref="OrderingDbContext"/> (to read an
/// order's current total, never trusting a client-sent one) and, when a tip
/// is attributed, <see cref="IdentityDbContext"/> — the same three-module
/// composition shape <c>OrderEndpoints.AuthorizeManagerAsync</c> already uses
/// for Ordering+Identity, layered onto the Catalog+Identity shape
/// <c>PriceListEndpoints</c> established — see
/// <c>docs/architecture/module-boundaries.md</c> rule 5.
/// </summary>
/// <remarks>
/// <b>Purely additive — does not gate <c>Order.Close()</c>.</b> See
/// <see cref="Payment"/>'s own class remarks for why wiring a payment
/// requirement into the already-proven close path is a deliberately
/// separate, later decision, not a side effect of shipping the record.
/// </remarks>
public static class PaymentEndpoints
{
    /// <summary>Maps the payment endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapPaymentEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/orders/{orderId:guid}/payments", RecordPaymentAsync)
            .WithName("RecordPayment")
            .WithSummary("Records a cash or card tender against an order's remaining balance, computing change (PAY-01/02/03) — a card tender cannot exceed the balance, since a TPA has no change to give back. A tender smaller than what's owed is a valid partial payment (PAY-05); recording several tenders across different methods as one atomic action is POST .../payments/split (PAY-04). An optional tip, attributed to a staff member or left unattributed, rides along on the same payment (PAY-06).")
            .Produces<PaymentDto>(StatusCodes.Status201Created);

        group.MapPost("/orders/{orderId:guid}/payments/split", RecordSplitPaymentAsync)
            .WithName("RecordSplitPayment")
            .WithSummary("Records several tenders — any mix of methods — against an order's remaining balance as one atomic action (PAY-04). Each tender applies in order against what's still owed after the ones before it in the same batch; if any tender is rejected, nothing in the batch is persisted. No tip support here — attribute a tip on an ordinary single tender instead (PAY-06).")
            .Produces<List<PaymentDto>>(StatusCodes.Status201Created);

        group.MapGet("/orders/{orderId:guid}/payments", GetPaymentsAsync)
            .WithName("GetPayments")
            .WithSummary("Lists every payment recorded against an order, oldest first.")
            .Produces<List<PaymentDto>>();

        return group;
    }

    private static async Task<IResult> RecordPaymentAsync(
        Guid orderId,
        RecordPaymentRequest request,
        PaymentsDbContext paymentsDb,
        OrderingDbContext orderingDb,
        IdentityDbContext identityDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var order = await orderingDb.Orders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Modifiers)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Error.NotFound("order.not_found", $"Order {orderId} was not found.").ToProblem();
        }

        var amountDue = await RemainingBalanceAsync(orderId, order.Total, paymentsDb, cancellationToken).ConfigureAwait(false);

        var built = await BuildTenderAsync(
            orderId, request.Method, request.AmountTendered, request.TipAmount, request.StaffId, request.CashSessionId,
            amountDue, paymentsDb, identityDb, clock, cancellationToken).ConfigureAwait(false);

        if (built.IsFailure)
        {
            return built.Error.ToProblem();
        }

        paymentsDb.Payments.Add(built.Value.Payment);
        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created(
            $"/api/v1/orders/{orderId}/payments/{built.Value.Payment.Id}",
            built.Value.Payment.ToDto(built.Value.AttributedStaffName));
    }

    private static async Task<IResult> RecordSplitPaymentAsync(
        Guid orderId,
        RecordSplitPaymentRequest request,
        PaymentsDbContext paymentsDb,
        OrderingDbContext orderingDb,
        IdentityDbContext identityDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (request.Tenders is null || request.Tenders.Count == 0)
        {
            return Error.Validation(
                "payment.empty_split", "A split payment needs at least one tender.").ToProblem();
        }

        var order = await orderingDb.Orders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Modifiers)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Error.NotFound("order.not_found", $"Order {orderId} was not found.").ToProblem();
        }

        var remaining = await RemainingBalanceAsync(orderId, order.Total, paymentsDb, cancellationToken).ConfigureAwait(false);

        // Nothing is added to paymentsDb until every tender in the batch has
        // validated successfully — the first failure returns immediately
        // with nothing persisted, since SaveChangesAsync is never reached.
        // A single SaveChangesAsync call for the whole batch is already one
        // implicit EF Core transaction, so this is atomic by construction,
        // no explicit BeginTransactionAsync needed.
        var built = new List<(Payment Payment, string? AttributedStaffName)>();
        foreach (var tender in request.Tenders)
        {
            var result = await BuildTenderAsync(
                orderId, tender.Method, tender.AmountTendered, tipAmountRaw: 0, staffId: null, request.CashSessionId,
                remaining, paymentsDb, identityDb, clock, cancellationToken).ConfigureAwait(false);

            if (result.IsFailure)
            {
                return result.Error.ToProblem();
            }

            built.Add((result.Value.Payment, result.Value.AttributedStaffName));
            remaining = result.Value.Payment.RemainingBalance;
        }

        paymentsDb.Payments.AddRange(built.Select(b => b.Payment));
        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var dtos = built.Select(b => b.Payment.ToDto(b.AttributedStaffName)).ToList();
        return Results.Created($"/api/v1/orders/{orderId}/payments", dtos);
    }

    /// <summary>
    /// The order's own total minus every prior payment's <c>AmountApplied</c>
    /// (PAY-05) — never the order's full total by itself, since earlier
    /// tenders are already off the guest's tab.
    /// </summary>
    private static async Task<Money> RemainingBalanceAsync(
        Guid orderId, Money orderTotal, PaymentsDbContext paymentsDb, CancellationToken cancellationToken)
    {
        var priorPayments = await paymentsDb.Payments
            .Where(p => p.OrderId == orderId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return orderTotal - Money.Sum(priorPayments.Select(p => p.AmountApplied));
    }

    /// <summary>
    /// Validates and constructs one tender against a known remaining
    /// balance, without touching <see cref="PaymentsDbContext"/> — shared by
    /// the single-tender and split (PAY-04) endpoints so both apply exactly
    /// the same rules (method, amount, tip, card no-overpay, staff
    /// attribution) rather than two hand-maintained copies that could drift.
    /// </summary>
    private static async Task<Result<(Payment Payment, string? AttributedStaffName)>> BuildTenderAsync(
        Guid orderId,
        string methodRaw,
        decimal amountTenderedRaw,
        decimal tipAmountRaw,
        Guid? staffId,
        Guid? cashSessionId,
        Money amountDue,
        PaymentsDbContext paymentsDb,
        IdentityDbContext identityDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<PaymentMethod>(methodRaw, ignoreCase: true, out var method))
        {
            return Result.Failure<(Payment, string?)>(Error.Validation(
                "payment.unsupported_method", $"\"{methodRaw}\" is not a supported payment method yet."));
        }

        if (amountTenderedRaw <= 0)
        {
            return Result.Failure<(Payment, string?)>(Error.Validation(
                "payment.invalid_amount_tendered", "Amount tendered must be greater than zero."));
        }

        if (tipAmountRaw < 0)
        {
            return Result.Failure<(Payment, string?)>(Error.Validation(
                "payment.invalid_tip_amount", "Tip amount must not be negative."));
        }

        if (!amountDue.IsPositive)
        {
            return Result.Failure<(Payment, string?)>(Error.Validation(
                "payment.already_settled",
                $"Order {orderId} is already fully paid — nothing remains to tender against."));
        }

        // Attribution is optional even when a tip is given (PAY-06) — an
        // unattributed tip goes to a shared pool — but a *named* staff id
        // must be real, the same "confirm before constructing" shape
        // FloorEndpoints.AssignRoomSectionAsync already uses for FLR-06.
        string? attributedStaffName = null;
        if (staffId is { } id)
        {
            var staff = await identityDb.Staff
                .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (staff is null)
            {
                return Result.Failure<(Payment, string?)>(
                    Error.NotFound("identity.staff_not_found", $"Staff member {id} was not found."));
            }

            attributedStaffName = staff.Name;
        }

        // Optional, unvalidated by Payment itself (PAY-11) — but a *named*
        // session must be real, the same "confirm before constructing"
        // shape this method already uses for staff attribution just above.
        if (cashSessionId is { } sessionId)
        {
            var sessionExists = await paymentsDb.CashSessions
                .AnyAsync(c => c.Id == sessionId, cancellationToken)
                .ConfigureAwait(false);

            if (!sessionExists)
            {
                return Result.Failure<(Payment, string?)>(
                    Error.NotFound("cash_session.not_found", $"Cash session {sessionId} was not found."));
            }
        }

        var amountTendered = Money.FromDecimal(amountTenderedRaw);

        // A standalone TPA (PAY-03) charges exactly the amount keyed in —
        // there is no "change" mechanism a card terminal can hand back the
        // way a cash drawer can. Mirrors the same guard Payment's own
        // constructor enforces, so the API returns a proper Result/Error
        // (hard rule 5) instead of letting that ArgumentException bubble.
        if (method == PaymentMethod.Card && amountTendered > amountDue)
        {
            return Result.Failure<(Payment, string?)>(Error.Validation(
                "payment.card_tender_exceeds_balance",
                "A card tender cannot exceed what's still owed — cards have no change."));
        }

        var tipAmount = Money.FromDecimal(tipAmountRaw);
        var payment = new Payment(orderId, method, amountDue, amountTendered, tipAmount, staffId, clock.UtcNow, cashSessionId);
        return (payment, attributedStaffName);
    }

    private static async Task<IResult> GetPaymentsAsync(
        Guid orderId,
        PaymentsDbContext paymentsDb,
        OrderingDbContext orderingDb,
        IdentityDbContext identityDb,
        CancellationToken cancellationToken)
    {
        var orderExists = await orderingDb.Orders
            .AnyAsync(o => o.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (!orderExists)
        {
            return Error.NotFound("order.not_found", $"Order {orderId} was not found.").ToProblem();
        }

        var payments = await paymentsDb.Payments
            .Where(p => p.OrderId == orderId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // One batched Identity query for every distinct AttributedStaffId
        // across however many payments are being listed, not N+1 — the same
        // shape FloorEndpoints.ResolveStaffNamesAsync already uses for
        // RoomDto.AssignedStaffName.
        var distinctStaffIds = payments
            .Where(p => p.AttributedStaffId is not null)
            .Select(p => p.AttributedStaffId!.Value)
            .Distinct()
            .ToList();
        var staffNamesById = distinctStaffIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await identityDb.Staff
                .Where(s => distinctStaffIds.Contains(s.Id))
                .ToDictionaryAsync(s => s.Id, s => s.Name, cancellationToken)
                .ConfigureAwait(false);

        // Sorted client-side: SQLite's EF Core provider (ADR 0012) cannot
        // translate an ORDER BY over DateTimeOffset (PaidAtUtc) — the same
        // limitation TaxRuleEndpoints.GetTaxRulesAsync already works around.
        var ordered = payments
            .OrderBy(p => p.PaidAtUtc)
            .Select(p => p.ToDto(
                p.AttributedStaffId is { } staffId && staffNamesById.TryGetValue(staffId, out var name) ? name : null))
            .ToList();

        return Results.Ok(ordered);
    }
}
