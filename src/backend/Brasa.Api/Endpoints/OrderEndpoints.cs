using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Domain;
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

        group.MapGet("/orders", SearchOrdersAsync)
            .WithName("SearchOrders")
            .WithSummary("Order history/search — filter by status, table and opened-date range (ORD-22).");

        group.MapGet("/orders/{orderId:guid}", GetOrderAsync)
            .WithName("GetOrder")
            .WithSummary("Current state of an order.");

        group.MapPost("/orders/{orderId:guid}/lines", AddLineAsync)
            .WithName("AddOrderLine")
            .WithSummary("Rings up a menu item onto an open order.");

        group.MapPut("/orders/{orderId:guid}/lines/{lineId:guid}/notes", SetLineNotesAsync)
            .WithName("SetOrderLineNotes")
            .WithSummary("Sets or clears a line's free-text kitchen note (ORD-06).");

        group.MapGet("/orders/{orderId:guid}/split", PreviewSplitAsync)
            .WithName("PreviewOrderSplit")
            .WithSummary("Computes an even split of the current total, without changing order state.");

        group.MapGet("/orders/{orderId:guid}/pre-bill", GetPreBillAsync)
            .WithName("GetOrderPreBill")
            .WithSummary("Pre-bill preview for the table — a documento não fiscal, not an invoice. Safe to call repeatedly.");

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

        // Floor saves before Ordering here, deliberately — the reverse of
        // CloseOrderAsync below. table.State is guarded by an xmin
        // concurrency token (TableConfiguration.cs): two requests can both
        // read this row as Free and both transition it in memory, but only
        // one SaveChangesAsync can win — the loser's blind UPDATE-by-id now
        // has "AND xmin = @original" in its WHERE clause, matches zero rows,
        // and EF throws DbUpdateConcurrencyException instead of silently
        // overwriting the winner. Saving Floor first means that failure
        // happens before the Order is ever created, so a lost race leaves
        // nothing behind to clean up — no orphan order, just a clean 409.
        try
        {
            await floorDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Error.Conflict("floor.table_not_free", $"Table {table.Label} is not free.").ToProblem();
        }

        var order = Order.Open(table.Id, table.Label, request.CoverCount, clock.UtcNow);
        orderingDb.Orders.Add(order);
        await orderingDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Created($"/api/v1/orders/{order.Id}", order.ToDto());
    }

    /// <summary>
    /// Order history/search (ORD-22). Returns the lightest shape that can
    /// still identify and total each order — <see cref="OrderSummaryDto"/>,
    /// not the full line-by-line <see cref="OrderDto"/> — because a history
    /// list is read by the page, not the order. No dedicated read model yet
    /// (that's RPT, I8); this queries Ordering's own table directly, which is
    /// still within bounds for I2's scale.
    /// </summary>
    private static async Task<IResult> SearchOrdersAsync(
        OrderingDbContext db,
        string? status,
        Guid? tableId,
        DateTimeOffset? openedFrom,
        DateTimeOffset? openedTo,
        CancellationToken cancellationToken,
        int take = 50)
    {
        OrderStatus? statusFilter = null;
        if (!string.IsNullOrEmpty(status))
        {
            if (!Enum.TryParse<OrderStatus>(status, ignoreCase: true, out var parsed))
            {
                return Error.Validation(
                    "order.invalid_status_filter", $"\"{status}\" is not a recognised order status.").ToProblem();
            }

            statusFilter = parsed;
        }

        if (take is < 1 or > 200)
        {
            return Error.Validation("order.invalid_take", "take must be between 1 and 200.").ToProblem();
        }

        var query = db.Orders.Include(o => o.Lines).ThenInclude(l => l.Modifiers).AsQueryable();

        if (statusFilter is not null)
        {
            query = query.Where(o => o.Status == statusFilter);
        }

        if (tableId is not null)
        {
            query = query.Where(o => o.TableId == tableId);
        }

        if (openedFrom is not null)
        {
            query = query.Where(o => o.OpenedAtUtc >= openedFrom);
        }

        if (openedTo is not null)
        {
            query = query.Where(o => o.OpenedAtUtc <= openedTo);
        }

        var orders = await query
            .OrderByDescending(o => o.OpenedAtUtc)
            .Take(take)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(orders.Select(o => o.ToSummaryDto()).ToArray());
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
            .Include(i => i.ModifierGroups)
            .ThenInclude(g => g.Modifiers)
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

        var modifiersResult = ResolveModifiers(menuItem, request.SelectedModifierIds ?? []);
        if (modifiersResult.IsFailure)
        {
            return modifiersResult.Error.ToProblem();
        }

        // The line snapshots price, VAT rate and modifiers now — see
        // docs/architecture/module-boundaries.md on why that is correctness,
        // not denormalisation.
        var addResult = order.AddLine(
            menuItem.Id, menuItem.Name, menuItem.Price, menuItem.VatRate.Fraction, request.Quantity, modifiersResult.Value);
        if (addResult.IsFailure)
        {
            return addResult.Error.ToProblem();
        }

        await orderingDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Results.Ok(order.ToDto());
    }

    private static async Task<IResult> SetLineNotesAsync(
        Guid orderId,
        Guid lineId,
        SetLineNotesRequest request,
        OrderingDbContext db,
        CancellationToken cancellationToken)
    {
        var order = await FindOrderAsync(db, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderNotFound(orderId).ToProblem();
        }

        var result = order.SetLineNotes(lineId, request.Notes);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task<IResult> GetPreBillAsync(
        Guid orderId,
        OrderingDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var order = await FindOrderAsync(db, orderId, cancellationToken).ConfigureAwait(false);
        if (order is null)
        {
            return OrderNotFound(orderId).ToProblem();
        }

        var guardResult = order.EnsureCanGeneratePreBill();
        if (guardResult.IsFailure)
        {
            return guardResult.Error.ToProblem();
        }

        // FiscalDocumentLine is reused purely as a gross-inclusive net/VAT
        // calculator (Brasa.Modules.Fiscal) — IFiscalProvider is never
        // called, so nothing is issued or numbered. A pre-bill is a
        // documento não fiscal; see EnsureCanGeneratePreBill and PreBillDto.
        var fiscalLines = order.Lines
            .Select(l => new FiscalDocumentLine(l.ItemName, l.Quantity, l.UnitPrice + l.ModifiersTotal, l.VatRateFraction))
            .ToArray();

        return Results.Ok(order.ToPreBillDto(fiscalLines, clock.UtcNow));
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

        // ModifiersTotal is folded into the unit price handed to Fiscal — the
        // guest's gross total must include selected modifiers, and the mock
        // (and, later, real) engine only knows about one VAT-inclusive unit
        // price per line. Itemising modifiers as their own fiscal lines is
        // real fiscal-engine work (I7), not needed for I1's correctness bar:
        // order.Total and document.GrossTotal must still agree to the cent.
        var fiscalLines = order.Lines
            .Select(l => new FiscalDocumentLine(l.ItemName, l.Quantity, l.UnitPrice + l.ModifiersTotal, l.VatRateFraction))
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
        // moved on somehow, this save fails, or another request wins the
        // same xmin race Occupy uses — see OpenOrderAsync), the close itself
        // still stands and the response below is still correct. Staff can
        // always clear a stuck table by hand; they can never recover a lost
        // fiscal document.
        var table = await floorDb.Tables
            .FirstOrDefaultAsync(t => t.Id == order.TableId, cancellationToken)
            .ConfigureAwait(false);

        if (table is not null && table.MarkDirty().IsSuccess)
        {
            try
            {
                await floorDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException)
            {
                // Best-effort, as above — swallow and move on.
            }
        }

        return Results.Ok(new CloseOrderResponse(order.ToDto(), fiscalResult.Value.ToDto()));
    }

    private static Task<Order?> FindOrderAsync(OrderingDbContext db, Guid orderId, CancellationToken cancellationToken)
        => db.Orders
            .Include(o => o.Lines)
            .ThenInclude(l => l.Modifiers)
            .FirstOrDefaultAsync(o => o.Id == orderId, cancellationToken);

    private static Error OrderNotFound(Guid orderId)
        => Error.NotFound("order.not_found", $"Order {orderId} was not found.");

    /// <summary>
    /// Resolves the requested modifier ids against <paramref name="menuItem"/>'s
    /// own modifier groups and enforces each group's min/max selection count.
    /// Catalog never crosses into Ordering, and Ordering never resolves a
    /// modifier id itself — this is the API layer doing exactly the
    /// composition job it exists for.
    /// </summary>
    private static Result<IReadOnlyList<SelectedModifier>> ResolveModifiers(
        MenuItem menuItem,
        IReadOnlyList<Guid> selectedModifierIds)
    {
        var allModifiers = menuItem.ModifierGroups.SelectMany(g => g.Modifiers).ToDictionary(m => m.Id);

        var unknownId = selectedModifierIds.FirstOrDefault(id => !allModifiers.ContainsKey(id));
        if (unknownId != Guid.Empty)
        {
            return Result.Failure<IReadOnlyList<SelectedModifier>>(
                Error.NotFound("catalog.modifier_not_found", $"Modifier {unknownId} was not found on {menuItem.Name}."));
        }

        foreach (var group in menuItem.ModifierGroups)
        {
            var selectedCount = group.Modifiers.Count(m => selectedModifierIds.Contains(m.Id));
            if (selectedCount < group.MinSelect || selectedCount > group.MaxSelect)
            {
                return Result.Failure<IReadOnlyList<SelectedModifier>>(Error.Validation(
                    "catalog.modifier_selection_invalid",
                    $"\"{group.Name}\" needs between {group.MinSelect} and {group.MaxSelect} selection(s); got {selectedCount}."));
            }
        }

        IReadOnlyList<SelectedModifier> resolved = [.. selectedModifierIds
            .Select(id => allModifiers[id])
            .Select(m => new SelectedModifier(m.Id, m.Name, m.PriceDelta))];

        return Result.Success(resolved);
    }
}
