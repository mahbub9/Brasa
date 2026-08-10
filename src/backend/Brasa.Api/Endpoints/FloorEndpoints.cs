using Brasa.Api.Contracts;
using Brasa.Modules.Floor.Domain;
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

        group.MapPost("/rooms/{roomId:guid}/tables", CreateTableAsync)
            .WithName("CreateTable")
            .WithSummary("Adds a table to a room (FLR-03).");

        group.MapPut("/tables/{tableId:guid}", UpdateTableAsync)
            .WithName("UpdateTable")
            .WithSummary("Edits a table's label, seats, shape or position (FLR-03).");

        group.MapDelete("/tables/{tableId:guid}", DeleteTableAsync)
            .WithName("DeleteTable")
            .WithSummary("Removes a table from the floor plan. Requires the table to be free (FLR-03).");

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

    /// <summary>
    /// Adds a table to a room (FLR-03) — the first slice of the floor-plan
    /// editor named in this task's own roadmap row. Position/shape were
    /// seeded onto <c>Table</c> since I1 for exactly this (see that type's
    /// own remarks); this ships the mutation, not yet the drag-and-drop
    /// canvas — <c>admin</c> submits plain numeric position fields today, no
    /// visual editor. Room creation is a deliberate, separate gap: only
    /// tables within a room that already exists (seeded, FLR-01) can be
    /// added — see the feature's own "Open questions".
    /// </summary>
    private static async Task<IResult> CreateTableAsync(
        Guid roomId,
        CreateTableRequest request,
        FloorDbContext db,
        CancellationToken cancellationToken)
    {
        var room = await db.Rooms.FirstOrDefaultAsync(r => r.Id == roomId, cancellationToken).ConfigureAwait(false);
        if (room is null)
        {
            return Error.NotFound("floor.room_not_found", $"Room {roomId} was not found.").ToProblem();
        }

        if (string.IsNullOrWhiteSpace(request.Label))
        {
            return Error.Validation("floor.invalid_label", "Table label must not be empty.").ToProblem();
        }

        if (request.Seats < 1)
        {
            return Error.Validation("floor.invalid_seats", "Seats must be at least 1.").ToProblem();
        }

        var shapeResult = ParseTableShape(request.Shape);
        if (shapeResult.IsFailure)
        {
            return shapeResult.Error.ToProblem();
        }

        var table = new Table(roomId, request.Label.Trim(), request.Seats, request.PositionX, request.PositionY, shapeResult.Value);
        db.Tables.Add(table);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(table.ToDto());
    }

    /// <summary>Edits a table's label, seats, shape or position (FLR-03). Independent of the table's current state.</summary>
    private static async Task<IResult> UpdateTableAsync(
        Guid tableId,
        UpdateTableRequest request,
        FloorDbContext db,
        CancellationToken cancellationToken)
    {
        var table = await db.Tables.FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken).ConfigureAwait(false);
        if (table is null)
        {
            return Error.NotFound("floor.table_not_found", $"Table {tableId} was not found.").ToProblem();
        }

        var shapeResult = ParseTableShape(request.Shape);
        if (shapeResult.IsFailure)
        {
            return shapeResult.Error.ToProblem();
        }

        var updateResult = table.Update(request.Label, request.Seats, request.PositionX, request.PositionY, shapeResult.Value);
        if (updateResult.IsFailure)
        {
            return updateResult.Error.ToProblem();
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone else edited this same table between our read and our
            // write — see TableConfiguration.cs. Unlike ClearTableAsync/
            // RequestBillAsync, there's no state precondition to re-affirm
            // here (editing fields doesn't require any particular
            // TableState), so this is a genuine lost-update race, not a
            // stale-state check — report it as such rather than reusing one
            // of the state-specific codes.
            return Error.Conflict(
                "floor.table_concurrently_modified",
                $"Table {table.Label} was changed by someone else. Reload and try again.").ToProblem();
        }

        return Results.Ok(table.ToDto());
    }

    /// <summary>Removes a table from the floor plan. Requires it to be free (FLR-03) — see <see cref="Table.EnsureCanDelete"/>.</summary>
    private static async Task<IResult> DeleteTableAsync(Guid tableId, FloorDbContext db, CancellationToken cancellationToken)
    {
        var table = await db.Tables.FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken).ConfigureAwait(false);
        if (table is null)
        {
            return Error.NotFound("floor.table_not_found", $"Table {tableId} was not found.").ToProblem();
        }

        var ensureResult = table.EnsureCanDelete();
        if (ensureResult.IsFailure)
        {
            return ensureResult.Error.ToProblem();
        }

        db.Tables.Remove(table);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Someone occupied (or otherwise changed) this table between our
            // read and our delete — same re-affirm-the-precondition shape as
            // ClearTableAsync/RequestBillAsync.
            return Error.Conflict("floor.table_not_free", $"Table {table.Label} is not free.").ToProblem();
        }

        return Results.NoContent();
    }

    private static Result<TableShape> ParseTableShape(string shape)
    {
        return Enum.TryParse<TableShape>(shape, ignoreCase: true, out var parsed)
            ? Result.Success(parsed)
            : Result.Failure<TableShape>(
                Error.Validation("floor.invalid_shape", $"\"{shape}\" is not a recognised table shape."));
    }
}
