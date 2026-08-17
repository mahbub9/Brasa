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
/// Cash-tender endpoints (PAY-01/02/05/06). Composes <see cref="PaymentsDbContext"/>
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
            .WithSummary("Records a cash tender against an order's remaining balance, computing change (PAY-01/02). A tender smaller than what's owed is a valid partial payment (PAY-05); splitting one payment across several methods at once is still PAY-04. An optional tip, attributed to a staff member or left unattributed, rides along on the same payment (PAY-06).")
            .Produces<PaymentDto>(StatusCodes.Status201Created);

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
        if (!Enum.TryParse<PaymentMethod>(request.Method, ignoreCase: true, out var method))
        {
            return Error.Validation(
                "payment.unsupported_method", $"\"{request.Method}\" is not a supported payment method yet.").ToProblem();
        }

        if (request.AmountTendered <= 0)
        {
            return Error.Validation(
                "payment.invalid_amount_tendered", "Amount tendered must be greater than zero.").ToProblem();
        }

        if (request.TipAmount < 0)
        {
            return Error.Validation(
                "payment.invalid_tip_amount", "Tip amount must not be negative.").ToProblem();
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

        // The remaining balance, not always the order's full total (PAY-05)
        // — every prior payment's own AmountApplied is already off the
        // guest's tab, so a second (or third) tender only needs to cover
        // what's left, never the whole order again.
        var priorPayments = await paymentsDb.Payments
            .Where(p => p.OrderId == orderId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var amountAlreadyApplied = Money.Sum(priorPayments.Select(p => p.AmountApplied));
        var amountDue = order.Total - amountAlreadyApplied;

        if (!amountDue.IsPositive)
        {
            return Error.Validation(
                "payment.already_settled",
                $"Order {orderId} is already fully paid — nothing remains to tender against.").ToProblem();
        }

        // Attribution is optional even when a tip is given (PAY-06) — an
        // unattributed tip goes to a shared pool — but a *named* staff id
        // must be real, the same "confirm before constructing" shape
        // FloorEndpoints.AssignRoomSectionAsync already uses for FLR-06.
        string? attributedStaffName = null;
        if (request.StaffId is { } staffId)
        {
            var staff = await identityDb.Staff
                .FirstOrDefaultAsync(s => s.Id == staffId, cancellationToken)
                .ConfigureAwait(false);

            if (staff is null)
            {
                return Error.NotFound("identity.staff_not_found", $"Staff member {staffId} was not found.").ToProblem();
            }

            attributedStaffName = staff.Name;
        }

        var amountTendered = Money.FromDecimal(request.AmountTendered);
        var tipAmount = Money.FromDecimal(request.TipAmount);
        var payment = new Payment(orderId, method, amountDue, amountTendered, tipAmount, request.StaffId, clock.UtcNow);
        paymentsDb.Payments.Add(payment);
        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/orders/{orderId}/payments/{payment.Id}", payment.ToDto(attributedStaffName));
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
