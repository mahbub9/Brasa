using System.Globalization;
using Brasa.Api.Contracts;
using Brasa.Api.Csv;
using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Shared.Primitives;
using Brasa.Shared.Time;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>Menu endpoints — read for the POS, editing for <c>admin</c> (WEB-10).</summary>
/// <remarks>
/// <see cref="GetMenuAllAsync"/> is the one endpoint here <c>pos</c> never
/// calls — it's <c>admin</c>'s management view, unfiltered on purpose. Every
/// other endpoint is shared: <c>pos</c> only ever reads <c>GET /menu</c>,
/// <c>admin</c> is the only caller of the mutations. See
/// <c>docs/product/roadmap.md</c>.
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

        group.MapGet("/menu/all", GetMenuAllAsync)
            .WithName("GetMenuAll")
            .WithSummary("Every category and item, including hidden and unavailable ones, for management tooling.");

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

        group.MapPut("/menu/items/{itemId:guid}/takeaway-price", UpdateMenuItemTakeawayPriceAsync)
            .WithName("UpdateMenuItemTakeawayPrice")
            .WithSummary("Sets or clears a menu item's separate takeaway price (CAT-06). Null falls back to the dine-in price.");

        group.MapPut("/menu/items/{itemId:guid}/course", UpdateMenuItemCourseAsync)
            .WithName("UpdateMenuItemCourse")
            .WithSummary("Sets or clears which course a menu item is served at (CAT-14).");

        group.MapPut("/menu/items/{itemId:guid}/station", UpdateMenuItemStationAsync)
            .WithName("UpdateMenuItemStation")
            .WithSummary("Sets or clears which kitchen station prepares a menu item (CAT-15).");

        group.MapPut("/menu/items/{itemId:guid}/schedule", UpdateMenuItemScheduleAsync)
            .WithName("UpdateMenuItemSchedule")
            .WithSummary("Sets or clears a menu item's recurring day/time availability window (CAT-11).");

        group.MapPut("/menu/categories/{categoryId:guid}/visibility", UpdateMenuCategoryVisibilityAsync)
            .WithName("UpdateMenuCategoryVisibility")
            .WithSummary("Hides a whole category (and every item under it) from GET /menu, or shows it again (CAT-01).");

        return group;
    }

    private static async Task<IResult> GetMenuAsync(
        HttpContext httpContext, CatalogDbContext db, IClock clock, CancellationToken cancellationToken)
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

        // CAT-11: a scheduled item (prato do dia) only belongs on the
        // guest-facing menu during its own window. Evaluated in memory, not
        // translated to SQL — items are already materialised above, and
        // there is no per-tenant region/site record yet to pick a timezone
        // from (that's IDN-01/CAT-05 territory), so this uses mainland
        // Portugal time, the same default the rest of the app assumes
        // absent a real site.
        var localNow = PortugueseRegion.Continental.ToLocal(clock.UtcNow);
        var dayOfWeek = localNow.DayOfWeek;
        var timeOfDay = TimeOnly.FromDateTime(localNow.DateTime);

        var itemsByCategory = items
            .Where(i => i.IsAvailableAt(dayOfWeek, timeOfDay))
            .ToLookup(i => i.CategoryId);

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

    /// <summary>
    /// Every category and item, hidden/unavailable ones included (WEB-10).
    /// <c>GET /menu</c> exists to render a guest-facing menu, so it filters
    /// to what a guest may actually order — which means it can never be the
    /// data source for a management screen that needs to *show* a hidden
    /// category so staff can turn it back on. That's a real gap, not a
    /// hypothetical one: without this endpoint, hiding a category or
    /// 86'ing an item would be a one-way door once the admin UI's only view
    /// of the catalog was the already-filtered one.
    /// </summary>
    private static async Task<IResult> GetMenuAllAsync(CatalogDbContext db, CancellationToken cancellationToken)
    {
        var categories = await db.Categories
            .OrderBy(c => c.DisplayOrder)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = await db.Items
            .Include(i => i.ModifierGroups)
            .ThenInclude(g => g.Modifiers)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var itemsByCategory = items.ToLookup(i => i.CategoryId);

        var dto = categories
            .Select(c => new AdminMenuCategoryDto(
                c.Id,
                c.Name,
                c.DisplayOrder,
                c.IsVisible,
                [.. itemsByCategory[c.Id].Select(i => i.ToDto())]))
            .ToList();

        return Results.Ok(dto);
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
    /// called either one until now, so <see cref="MenuItem.IsAvailable"/>
    /// could never actually become <c>false</c>. Called from <c>admin</c>'s
    /// menu editor (WEB-10) today; <c>pos</c> has no in-order 86 control yet.
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
    /// doc comment. Called from <c>admin</c>'s menu editor (WEB-10) today.
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

    /// <summary>
    /// Sets or clears a menu item's separate takeaway price (CAT-06) — a
    /// dine-in item is often priced differently with no table service to
    /// cover. VAT rate is unaffected: this only changes which
    /// <see cref="Money"/> <c>AddLineAsync</c> snapshots onto the line, per
    /// <c>Order.IsTakeaway</c>, not the rate charged on it — that's a
    /// separate, not-yet-built <c>TaxRule</c> (CAT-07/08) concern. Delivery
    /// pricing (the third channel this row's own title names) isn't built
    /// either: there is no delivery order path in this codebase yet at all,
    /// so there is nothing for a delivery price to attach to.
    /// </summary>
    private static async Task<IResult> UpdateMenuItemTakeawayPriceAsync(
        Guid itemId,
        UpdateMenuItemTakeawayPriceRequest request,
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

        var price = request.Price is null ? (Money?)null : Money.FromDecimal(request.Price.Value);
        var result = item.SetTakeawayPrice(price);
        if (result.IsFailure)
        {
            return result.Error.ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }

    /// <summary>
    /// Sets or clears which course a menu item is served at (CAT-14) — a
    /// greenfield field with no prior gap to close, unlike CAT-13/19: the
    /// domain method, the endpoint and this field's very existence all
    /// arrive together. Ships ahead of its consumer the same way CAT-02's
    /// allergen set did: course *firing* (ORD-07) isn't built, so this is
    /// only a menu-display tag today, not yet a kitchen-sequencing signal.
    /// </summary>
    private static async Task<IResult> UpdateMenuItemCourseAsync(
        Guid itemId,
        UpdateMenuItemCourseRequest request,
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

        if (request.Course is null)
        {
            item.SetCourse(null);
        }
        else if (Enum.TryParse<Course>(request.Course, ignoreCase: true, out var course))
        {
            item.SetCourse(course);
        }
        else
        {
            return Error.Validation(
                "catalog.invalid_course", $"\"{request.Course}\" is not a recognised course.").ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }

    /// <summary>
    /// Sets or clears which kitchen station prepares a menu item (CAT-15) —
    /// same greenfield shape as CAT-14. Ships ahead of its consumer:
    /// station *routing* (KIT-06) needs printers and a KDS that don't exist
    /// yet, so this is only a menu-display/reporting tag today.
    /// </summary>
    private static async Task<IResult> UpdateMenuItemStationAsync(
        Guid itemId,
        UpdateMenuItemStationRequest request,
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

        if (request.Station is null)
        {
            item.SetStation(null);
        }
        else if (Enum.TryParse<KitchenStation>(request.Station, ignoreCase: true, out var station))
        {
            item.SetStation(station);
        }
        else
        {
            return Error.Validation(
                "catalog.invalid_station", $"\"{request.Station}\" is not a recognised kitchen station.").ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }

    /// <summary>
    /// Sets or clears a menu item's recurring day/time availability window
    /// (CAT-11) — a <i>prato do dia</i>. All three fields null/empty clears
    /// the schedule back to "always available"; setting only some of the
    /// three is rejected, since a schedule is all-or-nothing, not a partial
    /// update. Ships ahead of any UI trigger the same way CAT-14/15 did:
    /// <c>GetMenuAsync</c>'s own filter is the only consumer today.
    /// </summary>
    private static async Task<IResult> UpdateMenuItemScheduleAsync(
        Guid itemId,
        UpdateMenuItemScheduleRequest request,
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

        var hasDays = request.DaysOfWeek is { Count: > 0 };
        var hasTimes = request.StartTime is not null || request.EndTime is not null;

        if (!hasDays && !hasTimes)
        {
            item.SetSchedule(null);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Results.Ok(item.ToDto());
        }

        if (!hasDays || request.StartTime is null || request.EndTime is null)
        {
            return Error.Validation(
                "catalog.incomplete_schedule",
                "Days of week, start time and end time are all required together, or all omitted to clear the schedule.").ToProblem();
        }

        var days = ScheduleDays.None;
        foreach (var name in request.DaysOfWeek!)
        {
            if (!Enum.TryParse<DayOfWeek>(name, ignoreCase: true, out var dayOfWeek))
            {
                return Error.Validation(
                    "catalog.invalid_day_of_week", $"\"{name}\" is not a recognised day of the week.").ToProblem();
            }

            days |= dayOfWeek.ToScheduleDays();
        }

        if (!TimeOnly.TryParseExact(request.StartTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var startTime))
        {
            return Error.Validation(
                "catalog.invalid_time", $"\"{request.StartTime}\" is not a valid \"HH:mm\" time.").ToProblem();
        }

        if (!TimeOnly.TryParseExact(request.EndTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var endTime))
        {
            return Error.Validation(
                "catalog.invalid_time", $"\"{request.EndTime}\" is not a valid \"HH:mm\" time.").ToProblem();
        }

        try
        {
            item.SetSchedule(new MenuItemSchedule(days, startTime, endTime));
        }
        catch (ArgumentException ex)
        {
            return Error.Validation("catalog.invalid_schedule", ex.Message).ToProblem();
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(item.ToDto());
    }

    private static readonly string[] RequiredImportColumns = ["CategoryName", "Name", "Price", "VatRate"];

    /// <summary>
    /// Bulk-creates menu items from a CSV file (CAT-17). Rows import
    /// independently — one bad row (an unknown category, an unparsable
    /// price) is reported and skipped, it does not fail the whole request.
    /// Also the only way <c>admin</c>'s menu editor (WEB-10) can add a new
    /// item at all — there is still no "create item" endpoint.
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
    /// level up — a category, not an item). Called from <c>admin</c>'s menu
    /// editor (WEB-10) today.
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
