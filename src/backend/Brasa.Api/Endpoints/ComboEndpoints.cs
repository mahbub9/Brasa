using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Combo (<i>menu do dia</i>) CRUD endpoints (CAT-10) — a narrow first
/// slice, create/read/add-component only. Ringing a combo up onto an order
/// is <c>POST /orders/{id}/combo-lines</c>, in <c>OrderEndpoints.cs</c>
/// (composing this module with Ordering, the same shape <c>AddLineAsync</c>
/// already uses), not here.
/// </summary>
public static class ComboEndpoints
{
    /// <summary>Maps the combo endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapComboEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/combos", CreateComboAsync)
            .WithName("CreateCombo")
            .WithSummary("Creates an empty combo (CAT-10) — a fixed-price bundle of menu items.")
            .Produces<ComboDto>();

        group.MapGet("/combos", GetCombosAsync)
            .WithName("GetCombos")
            .WithSummary("Lists every combo for the current tenant.")
            .Produces<List<ComboDto>>();

        group.MapGet("/combos/{comboId:guid}", GetComboAsync)
            .WithName("GetCombo")
            .WithSummary("Gets a combo and every component it currently holds.")
            .Produces<ComboDto>();

        group.MapPost("/combos/{comboId:guid}/components", AddComboComponentAsync)
            .WithName("AddComboComponent")
            .WithSummary("Adds one component item to a combo. Rejects a second entry for the same item.")
            .Produces<ComboDto>();

        return group;
    }

    private static async Task<IResult> CreateComboAsync(CreateComboRequest request, CatalogDbContext db, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("catalog.invalid_combo_name", "Combo name must not be empty.").ToProblem();
        }

        Combo combo;
        try
        {
            combo = new Combo(request.Name.Trim(), Money.FromDecimal(request.Price));
        }
        catch (ArgumentException)
        {
            return Error.Validation("catalog.invalid_price", "Price must not be negative.").ToProblem();
        }

        db.Combos.Add(combo);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(combo.ToDto());
    }

    private static async Task<IResult> GetCombosAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var combos = await db.Combos
            .Include(c => c.Components)
            .OrderBy(c => c.CreatedAtUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Results.Ok(combos.Select(c => c.ToDto()).ToList());
    }

    private static async Task<IResult> GetComboAsync(Guid comboId, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var combo = await FindComboAsync(db, comboId, cancellationToken).ConfigureAwait(false);
        return combo is null ? ComboNotFound(comboId).ToProblem() : Results.Ok(combo.ToDto());
    }

    private static async Task<IResult> AddComboComponentAsync(
        Guid comboId,
        AddComboComponentRequest request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var combo = await FindComboAsync(db, comboId, cancellationToken).ConfigureAwait(false);
        if (combo is null)
        {
            return ComboNotFound(comboId).ToProblem();
        }

        var menuItemExists = await db.Items.AnyAsync(i => i.Id == request.MenuItemId, cancellationToken).ConfigureAwait(false);
        if (!menuItemExists)
        {
            return Error.NotFound("catalog.item_not_found", $"Menu item {request.MenuItemId} was not found.").ToProblem();
        }

        var componentResult = combo.AddComponent(request.MenuItemId);
        if (componentResult.IsFailure)
        {
            return componentResult.Error.ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(combo.ToDto());
    }

    internal static Task<Combo?> FindComboAsync(CatalogDbContext db, Guid comboId, CancellationToken cancellationToken)
        => db.Combos.Include(c => c.Components).FirstOrDefaultAsync(c => c.Id == comboId, cancellationToken);

    internal static Error ComboNotFound(Guid comboId)
        => Error.NotFound("catalog.combo_not_found", $"Combo {comboId} was not found.");
}
