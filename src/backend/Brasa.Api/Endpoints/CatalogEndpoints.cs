using System.Globalization;
using Brasa.Api.Contracts;
using Brasa.Api.Csv;
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

        group.MapPost("/menu/items/import", ImportMenuItemsAsync)
            .WithName("ImportMenuItems")
            .WithSummary("Bulk-creates menu items from a CSV file (CAT-17).");

        group.MapPut("/menu/items/{itemId:guid}/availability", UpdateMenuItemAvailabilityAsync)
            .WithName("UpdateMenuItemAvailability")
            .WithSummary("86's a menu item, or brings it back (CAT-13).");

        group.MapPut("/menu/items/{itemId:guid}/price", UpdateMenuItemPriceAsync)
            .WithName("UpdateMenuItemPrice")
            .WithSummary("Changes a menu item's price for future orders. Past order lines keep their own snapshot.");

        group.MapPut("/menu/categories/{categoryId:guid}/visibility", UpdateMenuCategoryVisibilityAsync)
            .WithName("UpdateMenuCategoryVisibility")
            .WithSummary("Hides a whole category (and every item under it) from GET /menu, or shows it again (CAT-01).");

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

    /// <summary>
    /// 86's a menu item, or brings it back (CAT-13). <c>MarkAvailable</c>/
    /// <c>MarkUnavailable</c> have existed on the domain since I0 and are
    /// already enforced on the ordering side (<c>AddLine</c> rejects an
    /// unavailable item with <c>catalog.item_unavailable</c>) — but nothing
    /// ever called either one, so <see cref="MenuItem.IsAvailable"/> could
    /// never actually become <c>false</c>. Ships ahead of any UI that will
    /// call it — neither the admin back-office (WEB-09) nor an in-order
    /// 86 control in <c>pos</c> exists yet — the same way CAT-02/CAT-17/
    /// CAT-18 shipped ahead of theirs.
    /// </summary>
    private static async Task<IResult> UpdateMenuItemAvailabilityAsync(
        Guid itemId,
        UpdateMenuItemAvailabilityRequest request,
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

        if (request.IsAvailable)
        {
            item.MarkAvailable();
        }
        else
        {
            item.MarkUnavailable();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }

    /// <summary>
    /// Changes a menu item's price for future orders (<c>MenuItem.Reprice</c>
    /// existed with its own validation since I0, with nothing calling it —
    /// found the same way as CAT-13's availability gap). Safe by
    /// construction, not by convention: <c>OrderLine.UnitPrice</c> snapshots
    /// the price at the moment a line is added, so repricing an item never
    /// rewrites what a past order charged — see <c>MenuItem.Price</c>'s own
    /// doc comment. Ships ahead of any UI that will call it, same as
    /// CAT-02/CAT-13/CAT-17/CAT-18.
    /// </summary>
    private static async Task<IResult> UpdateMenuItemPriceAsync(
        Guid itemId,
        UpdateMenuItemPriceRequest request,
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

        try
        {
            item.Reprice(Money.FromDecimal(request.Price));
        }
        catch (ArgumentException)
        {
            return Error.Validation("catalog.invalid_price", "Price must not be negative.").ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }

    private static readonly string[] RequiredImportColumns = ["CategoryName", "Name", "Price", "VatRate"];

    /// <summary>
    /// Bulk-creates menu items from a CSV file (CAT-17). Rows import
    /// independently — one bad row (an unknown category, an unparsable
    /// price) is reported and skipped, it does not fail the whole request.
    /// Ships ahead of any admin UI to upload a file from, the same way
    /// CAT-02/CAT-18 shipped ahead of theirs.
    /// </summary>
    private static async Task<IResult> ImportMenuItemsAsync(
        ImportMenuItemsRequest request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var rows = CsvParser.Parse(request.Csv);
        if (rows.Count == 0)
        {
            return Error.Validation("catalog.import_empty", "The CSV has no rows.").ToProblem();
        }

        var header = rows[0];
        var columnIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < header.Count; i++)
        {
            columnIndex[header[i].Trim()] = i;
        }

        var missingColumns = RequiredImportColumns.Where(c => !columnIndex.ContainsKey(c)).ToArray();
        if (missingColumns.Length > 0)
        {
            return Error.Validation(
                "catalog.import_invalid_header",
                $"CSV header is missing required column(s): {string.Join(", ", missingColumns)}.").ToProblem();
        }

        var hasDescription = columnIndex.TryGetValue("Description", out var descriptionIndex);
        var hasIsAlcoholic = columnIndex.TryGetValue("IsAlcoholic", out var isAlcoholicIndex);

        var categoriesByName = await db.Categories
            .ToDictionaryAsync(c => c.Name, c => c.Id, StringComparer.OrdinalIgnoreCase, cancellationToken)
            .ConfigureAwait(false);

        var errors = new List<ImportMenuItemsRowError>();
        var created = new List<MenuItem>();

        for (var rowNumber = 1; rowNumber < rows.Count; rowNumber++)
        {
            var row = rows[rowNumber];
            var displayRowNumber = rowNumber; // 1-indexed against data rows — the header doesn't count.

            string Field(int index) => index < row.Count ? row[index].Trim() : string.Empty;

            var categoryName = Field(columnIndex["CategoryName"]);
            var name = Field(columnIndex["Name"]);
            var priceText = Field(columnIndex["Price"]);
            var vatRateText = Field(columnIndex["VatRate"]);
            var description = hasDescription ? Field(descriptionIndex) : null;
            var isAlcoholicText = hasIsAlcoholic ? Field(isAlcoholicIndex) : string.Empty;

            if (!categoriesByName.TryGetValue(categoryName, out var categoryId))
            {
                errors.Add(new ImportMenuItemsRowError(displayRowNumber, $"Unknown category \"{categoryName}\"."));
                continue;
            }

            if (!decimal.TryParse(priceText, NumberStyles.Number, CultureInfo.InvariantCulture, out var priceAmount))
            {
                errors.Add(new ImportMenuItemsRowError(displayRowNumber, $"\"{priceText}\" is not a valid price."));
                continue;
            }

            if (!decimal.TryParse(vatRateText, NumberStyles.Number, CultureInfo.InvariantCulture, out var vatFraction))
            {
                errors.Add(new ImportMenuItemsRowError(displayRowNumber, $"\"{vatRateText}\" is not a valid VAT rate."));
                continue;
            }

            var isAlcoholic = isAlcoholicText.Equals("true", StringComparison.OrdinalIgnoreCase)
                || isAlcoholicText.Equals("yes", StringComparison.OrdinalIgnoreCase)
                || isAlcoholicText == "1";

            try
            {
                var item = new MenuItem(categoryId, name, Money.FromDecimal(priceAmount), new VatRate(vatFraction), isAlcoholic);
                if (!string.IsNullOrWhiteSpace(description))
                {
                    item.SetDescription(description);
                }

                created.Add(item);
            }
            catch (ArgumentException ex)
            {
                errors.Add(new ImportMenuItemsRowError(displayRowNumber, ex.Message));
            }
        }

        if (created.Count > 0)
        {
            db.Items.AddRange(created);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Results.Ok(new ImportMenuItemsResponse(created.Count, errors));
    }

    /// <summary>
    /// Hides a whole category from <c>GET /menu</c>, or shows it again
    /// (CAT-01 — its own title names "visibility" as in scope, and the
    /// epic was marked done, but <c>MenuCategory.IsVisible</c> had no
    /// setter at all: nothing could ever set it to anything but its
    /// default <c>true</c>. Same shape as FLR-04/CAT-13/CAT-19's gaps, one
    /// level up — a category, not an item). Ships ahead of any UI that
    /// will call it, same as the others.
    /// </summary>
    private static async Task<IResult> UpdateMenuCategoryVisibilityAsync(
        Guid categoryId,
        UpdateMenuCategoryVisibilityRequest request,
        CatalogDbContext db,
        CancellationToken cancellationToken)
    {
        var category = await db.Categories
            .FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken)
            .ConfigureAwait(false);

        if (category is null)
        {
            return Error.NotFound("catalog.category_not_found", $"Menu category {categoryId} was not found.").ToProblem();
        }

        if (request.IsVisible)
        {
            category.MarkVisible();
        }
        else
        {
            category.MarkHidden();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(new MenuCategoryVisibilityDto(category.Id, category.Name, category.IsVisible));
    }
}
