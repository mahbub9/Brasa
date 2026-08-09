using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Modules.Fiscal;
using Brasa.Modules.Floor.Persistence;
using Brasa.Modules.Ordering.Domain;
using Brasa.Modules.Ordering.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Tenancy;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// The walking-skeleton flow: open a table, ring up lines, preview a split,
/// close and be issued a fiscal document.
/// </summary>
/// <remarks>
/// This handler composes the Ordering, Catalog, Floor and Fiscal modules —
/// reading from more than one module's DbContext in a single request is
/// exactly what the API layer is for. The modules never do this to each other
/// directly. See <c>docs/architecture/module-boundaries.md</c>.
/// </remarks>
public static class OrderEndpoints
{
    /// <summary>Maps the order endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapOrderEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/orders", OpenOrderAsync)
            .WithName("OpenOrder")
            .WithSummary("Opens a table.");

        group.MapGet("/orders/{orderId:guid}", GetOrderAsync)
            .WithName("GetOrder")
            .WithSummary("Current state of an order.");

        group.MapPost("/orders/{orderId:guid}/lines", AddLineAsync)
            .WithName("AddOrderLine")
            .WithSummary("Rings up a menu item onto an open order.");

        group.MapGet("/orders/{orderId:guid}/split", PreviewSplitAsync)
            .WithName("PreviewOrderSplit")
            .WithSummary("Computes an even split of the current total, without changing order state.");

        group.MapPost("/orders/{orderId:guid}/close", CloseOrderAsync)
            .WithName("CloseOrder")
            .WithSummary("Closes the order and issues its fiscal document.");

        return group;
    }

    private static async Task<IResult> OpenOrderAsync(
        OpenOrderRequest request,
        OrderingDbContext orderingDb,
        FloorDbContext floorDb,
        IClock clock,
        CancellationToken cancellationToken)
    {
        if (request.CoverCount < 1)
        {
            return Error.Validation("order.invalid_cover_count", "Cover count must be at least 1.").ToProblem();
        }

        var table = await floorDb.Tables
            .FirstOrDefaultAsync(t => t.Id == request.TableId, cancellationToken)
            .ConfigureAwait(false);

        if (table is null)
        {
            return Error.NotFound("floor.table_not_found", $"Table {request.TableId} was not found.").ToProblem();
        }

        var occupyResult = table.Occupy();
        if (occupyResult.IsFailure)
        {
            return occupyResult.Error.ToProblem();
        }

        var order = Order.Open(table.Id, table.Label, request.CoverCount, clock.UtcNow);
        orderingDb.Orders.Add(order);

        // Ordering saves before Floor, deliberately. Two DbContexts means this
        // isn't one transaction — if the second save fails, "an order exists
        // but the table still shows Free" (fixable: it's just a stale floor
        // view, the order itself works) is a much smaller problem than "the
        // table is stuck Occupied with no order behind it" would be. Same
        // I0/I1-scope trade-off CloseOrderAsync already makes below; real
        // cross-module atomicity is outbox-based work for I5+.
        await orderingDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await floorDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/orders/{order.Id}", order.ToDto());
    }

    private static async Task<IResult> GetOrderAsync(Guid orderId, OrderingDbContext db, CancellationToken cancellationToken)
    {
        var order = await FindOrderAsync(db, orderId, cancellationToken).ConfigureAwait(false);
        return order is null
            ? OrderNotFound(orderId).ToProblem()
            : Results.Ok(order.ToDto());
    }

    private static async Task<IResult> AddLineAsync(
        Guid orderId,
        AddLineRequest request,
        OrderingDbContext orderingDb,
        CatalogDbContext catalogDb,
        CancellationToken cancellationToken)
    {
        if (request.Quantity < 1)
        {
            return Error.Validation("order.invalid_quantity", "Quantity must be at least 1.").ToProblem();
        }

        var order = await FindOrderAsync(orderingDb, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderNotFound(orderId).ToProblem();
        }

        var menuItem = await catalogDb.Items
            .FirstOrDefaultAsync(i => i.Id == request.MenuItemId, cancellationToken)
            .ConfigureAwait(false);

        if (menuItem is null)
        {
            return Error.NotFound("catalog.item_not_found", $"Menu item {request.MenuItemId} was not found.").ToProblem();
        }

        if (!menuItem.IsAvailable)
        {
            return Error.Conflict("catalog.item_unavailable", $"{menuItem.Name} is not currently available.").ToProblem();
        }

        // The line snapshots price and VAT rate now — see docs/architecture/module-boundaries.md
        // on why that is correctness, not denormalisation.
        var addResult = order.AddLine(menuItem.Id, menuItem.Name, menuItem.Price, menuItem.VatRate.Fraction, request.Quantity);
        if (addResult.IsFailure)
        {
            return addResult.Error.ToProblem();
        }

        await orderingDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(order.ToDto());
    }

    private static async Task<IResult> PreviewSplitAsync(
        Guid orderId,
        int parts,
        OrderingDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await FindOrderAsync(db, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderNotFound(orderId).ToProblem();
        }

        var splitResult = order.SplitEvenly(parts);
        return splitResult.IsFailure
            ? splitResult.Error.ToProblem()
            : Results.Ok(splitResult.Value.Select(m => m.ToDto()).ToArray());
    }

    private static async Task<IResult> CloseOrderAsync(
        Guid orderId,
        OrderingDbContext orderingDb,
        FloorDbContext floorDb,
        IFiscalProvider fiscalProvider,
        ITenantContext tenantContext,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var order = await FindOrderAsync(orderingDb, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderNotFound(orderId).ToProblem();
        }

        var closeResult = order.Close(clock.UtcNow);
        if (closeResult.IsFailure)
        {
            return closeResult.Error.ToProblem();
        }

        var fiscalLines = order.Lines
            .Select(l => new FiscalDocumentLine(l.ItemName, l.Quantity, l.UnitPrice, l.VatRateFraction))
            .ToArray();

        var fiscalRequest = new FiscalDocumentRequest(tenantContext.TenantId, order.Id, fiscalLines);
        var fiscalResult = await fiscalProvider
            .IssueSimplifiedInvoiceAsync(fiscalRequest, cancellationToken)
            .ConfigureAwait(false);

        if (fiscalResult.IsFailure)
        {
            // The Close() call above only mutated the tracked in-memory entity;
            // nothing is persisted yet. Returning here without SaveChangesAsync
            // means the order stays open in the database for a retry, rather
            // than closing without ever being issued a document. A durable
            // two-phase guarantee across Ordering and the real fiscal engine is
            // outbox-based work for I5+; this ordering is I0's correctness floor.
            return fiscalResult.Error.ToProblem();
        }

        await orderingDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The order is now closed and fiscally issued — the part that must not
        // fail silently already succeeded. Marking the table dirty is
        // housekeeping on top of that: if it doesn't apply (table already
        // moved on somehow, or this save fails), the close itself still stands
        // and the response below is still correct. Staff can always clear a
        // stuck table by hand; they can never recover a lost fiscal document.
        var table = await floorDb.Tables
            .FirstOrDefaultAsync(t => t.Id == order.TableId, cancellationToken)
            .ConfigureAwait(false);

        if (table is not null && table.MarkDirty().IsSuccess)
        {
            await floorDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Results.Ok(new CloseOrderResponse(order.ToDto(), fiscalResult.Value.ToDto()));
    }

    private static Task<Order?> FindOrderAsync(OrderingDbContext db, Guid orderId, CancellationToken cancellationToken)
        => db.Orders.Include(o => o.Lines).FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    private static Error OrderNotFound(Guid orderId)
        => Error.NotFound("order.not_found", $"Order {orderId} was not found.");
}
