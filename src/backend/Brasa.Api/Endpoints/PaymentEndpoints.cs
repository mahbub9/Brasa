using Brasa.Api.Contracts;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Modules.Payments.Domain;
using Brasa.Modules.Payments.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Cash-tender endpoints (PAY-01/02). Composes <see cref="PaymentsDbContext"/>
/// (this module's own table) and <see cref="OrderingDbContext"/> (to read an
/// order's current total, never trusting a client-sent one) at the API
/// layer, the same shape <c>PriceListEndpoints</c> already uses for
/// Catalog+Identity — see
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
            .WithSummary("Records a cash tender against an order, computing change (PAY-01/02). Full payment only — no partial tender yet (PAY-05).")
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

        var order = await orderingDb.Orders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Modifiers)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Error.NotFound("order.not_found", $"Order {orderId} was not found.").ToProblem();
        }

        var amountDue = order.Total;
        var amountTendered = Money.FromDecimal(request.AmountTendered);

        if (amountTendered < amountDue)
        {
            return Error.Validation(
                "payment.insufficient_tender",
                $"Amount tendered ({amountTendered}) is less than the amount due ({amountDue}). " +
                "Partial payment is not supported yet.").ToProblem();
        }

        var payment = new Payment(orderId, method, amountDue, amountTendered, clock.UtcNow);
        paymentsDb.Payments.Add(payment);
        await paymentsDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/orders/{orderId}/payments/{payment.Id}", payment.ToDto());
    }

    private static async Task<IResult> GetPaymentsAsync(
        Guid orderId,
        PaymentsDbContext paymentsDb,
        OrderingDbContext orderingDb,
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

        // Sorted client-side: SQLite's EF Core provider (ADR 0012) cannot
        // translate an ORDER BY over DateTimeOffset (PaidAtUtc) — the same
        // limitation TaxRuleEndpoints.GetTaxRulesAsync already works around.
        var ordered = payments.OrderBy(p => p.PaidAtUtc).Select(p => p.ToDto()).ToList();

        return Results.Ok(ordered);
    }
}
