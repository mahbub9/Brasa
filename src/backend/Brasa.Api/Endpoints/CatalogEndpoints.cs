using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>Menu endpoints — read for the POS, a soft-delete for CAT-18.</summary>
/// <remarks>
/// No admin UI calls <c>DELETE /menu/items/{id}</c> yet — there is no
/// back-office app (WEB-09) to call it from. It exists ahead of that UI the
/// same way CAT-01/02 shipped ahead of a menu-editing UI: seeded data today,
/// a real editor later. See <c>docs/product/roadmap.md</c>.
/// </remarks>
public static class CatalogEndpoints
{
    /// <summary>Maps the catalog endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/menu", GetMenuAsync)
            .WithName("GetMenu")
            .WithSummary("Every visible category and its available items, for the POS to render.");

        group.MapDelete("/menu/items/{itemId:guid}", DeleteMenuItemAsync)
            .WithName("DeleteMenuItem")
            .WithSummary("Soft-deletes a menu item. Past orders keep their own snapshot of its name and price.");

        group.MapPut("/menu/items/{itemId:guid}/details", UpdateMenuItemDetailsAsync)
            .WithName("UpdateMenuItemDetails")
            .WithSummary("Sets a menu item's description and declared allergens (CAT-02).");

        return group;
    }

    private static async Task<IResult> GetMenuAsync(HttpContext httpContext, CatalogDbContext db, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(c => c.IsVisible)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await db.Items
            .Where(i => i.IsAvailable)
            .Include(i => i.ModifierGroups)
            .ThenInclude(g => g.Modifiers)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var itemsByCategory = items.ToLookup(i => i.CategoryId);

        var dto = categories
            .Select(c => new MenuCategoryDto(
                c.Id,
                c.Name,
                c.DisplayOrder,
                [.. itemsByCategory[c.Id].Select(i => i.ToDto())]))
            .ToList();

        // API-10: the menu changes rarely, so most POS pulls should come
        // back as a bodyless 304 rather than the same JSON every time.
        return ETagResults.OkWithETag(httpContext, dto);
    }

    private static async Task<IResult> DeleteMenuItemAsync(
        Guid itemId,
        CatalogDbContext db,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var item = await db.Items
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return Error.NotFound("catalog.item_not_found", $"Menu item {itemId} was not found.").ToProblem();
        }

        item.Delete(clock.UtcNow);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.NoContent();
    }

    private static async Task<IResult> UpdateMenuItemDetailsAsync(
        Guid itemId,
        UpdateMenuItemDetailsRequest request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var item = await db.Items
            .Include(i => i.ModifierGroups)
            .ThenInclude(g => g.Modifiers)
            .FirstOrDefaultAsync(i => i.Id == itemId, cancellationToken)
            .ConfigureAwait(false);

        if (item is null)
        {
            return Error.NotFound("catalog.item_not_found", $"Menu item {itemId} was not found.").ToProblem();
        }

        var allergens = new List<Allergen>();
        foreach (var name in request.Allergens ?? [])
        {
            if (!Enum.TryParse<Allergen>(name, ignoreCase: true, out var allergen))
            {
                return Error.Validation(
                    "catalog.invalid_allergen", $"\"{name}\" is not a recognised allergen.").ToProblem();
            }

            allergens.Add(allergen);
        }

        item.SetDescription(request.Description);
        item.SetAllergens(allergens);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }
}
