using Brasa.Api.Contracts;
using Brasa.Modules.Floor.Persistence;
using Brasa.Shared.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>Floor plan endpoints — the rooms and tables the POS renders as a table picker.</summary>
public static class FloorEndpoints
{
    /// <summary>Maps the floor endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapFloorEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/floor", GetFloorAsync)
            .WithName("GetFloor")
            .WithSummary("Every room and its tables, with current state, for the POS to render as a table picker.");

        group.MapPost("/tables/{tableId:guid}/clear", ClearTableAsync)
            .WithName("ClearTable")
            .WithSummary("Marks a dirty table cleared and free for the next party.");

        group.MapPost("/tables/{tableId:guid}/request-bill", RequestBillAsync)
            .WithName("RequestBill")
            .WithSummary("Flags that the table has asked for the bill (FLR-04) — a floor-plan signal for staff, separate from actually previewing the bill (ORD-18/19).");

        return group;
    }

    private static async Task<IResult> GetFloorAsync(FloorDbContext db, CancellationToken cancellationToken)
    {
        var rooms = await db.Rooms
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tables = await db.Tables
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var tablesByRoom = tables.ToLookup(t => t.RoomId);

        var dto = rooms
            .Select(r => new RoomDto(r.Id, r.Name, r.DisplayOrder, [.. tablesByRoom[r.Id].Select(t => t.ToDto())]))
            .ToList();

        return Results.Ok(dto);
    }

    private static async Task<IResult> ClearTableAsync(Guid tableId, FloorDbContext db, CancellationToken cancellationToken)
    {
        var table = await db.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken)
            .ConfigureAwait(false);

        if (table is null)
        {
            return Error.NotFound("floor.table_not_found", $"Table {tableId} was not found.").ToProblem();
        }

        var clearResult = table.Clear();
        if (clearResult.IsFailure)
        {
            return clearResult.Error.ToProblem();
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else's request touched this table between our read and
            // our write — see TableConfiguration.cs. Whatever state it's in
            // now, it isn't the Dirty state we just checked, so report the
            // same conflict a stale in-memory check would have given.
            return Error.Conflict("floor.table_not_dirty", $"Table {table.Label} is not dirty.").ToProblem();
        }

        return Results.Ok(table.ToDto());
    }

    private static async Task<IResult> RequestBillAsync(Guid tableId, FloorDbContext db, CancellationToken cancellationToken)
    {
        var table = await db.Tables
            .FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken)
            .ConfigureAwait(false);

        if (table is null)
        {
            return Error.NotFound("floor.table_not_found", $"Table {tableId} was not found.").ToProblem();
        }

        var requestBillResult = table.RequestBill();
        if (requestBillResult.IsFailure)
        {
            return requestBillResult.Error.ToProblem();
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else's request touched this table between our read and
            // our write — see TableConfiguration.cs. Whatever state it's in
            // now, it isn't the Occupied state we just checked, so report the
            // same conflict a stale in-memory check would have given.
            return Error.Conflict("floor.table_not_occupied", $"Table {table.Label} is not occupied.").ToProblem();
        }

        return Results.Ok(table.ToDto());
    }
}
