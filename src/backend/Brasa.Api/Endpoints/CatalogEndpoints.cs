using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>Read-only menu endpoints the POS uses to render the ordering screen.</summary>
public static class CatalogEndpoints
{
    /// <summary>Maps the catalog endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapCatalogEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapGet("/menu", GetMenuAsync)
            .WithName("GetMenu")
            .WithSummary("Every visible category and its available items, for the POS to render.");

        return group;
    }

    private static async Task<IResult> GetMenuAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .Where(c => c.IsVisible)
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await db.Items
            .Where(i => i.IsAvailable)
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

        return Results.Ok(dto);
    }
}
