using Brasa.Api.Contracts;
using Brasa.Modules.Catalog.Domain;
using Brasa.Modules.Catalog.Persistence;
using Brasa.Modules.Identity.Persistence;
using Brasa.Shared.Primitives;
using Microsoft.EntityFrameworkCore;

namespace Brasa.Api.Endpoints;

/// <summary>
/// Per-site price list endpoints (CAT-05) — a narrow first slice, unblocked
/// by IDN-01's <c>Site</c>. Create, read and add-entry only; no
/// rename/delete/remove-entry yet. Composes <c>CatalogDbContext</c> (this
/// module's own tables) and <c>IdentityDbContext</c> (to confirm a
/// <c>siteId</c> is real) in the same handler — sanctioned at the API
/// layer, never inside either module itself. See
/// <c>docs/architecture/module-boundaries.md</c> rule 5.
/// </summary>
/// <remarks>
/// Nothing in <c>AddLine</c> or either web client resolves an effective
/// price through this yet — there is no site-selection concept in
/// <c>pos</c>/<c>admin</c> today, so wiring this into ordering would have no
/// way to know which site an order belongs to. <c>GetEffectivePriceAsync</c>
/// is the resolution logic itself, verified at the API level, ready for
/// whichever future task adds site selection to a client.
/// </remarks>
public static class PriceListEndpoints
{
    /// <summary>Maps the price list endpoints onto a versioned route group.</summary>
    public static RouteGroupBuilder MapPriceListEndpoints(this RouteGroupBuilder group)
    {
        ArgumentNullException.ThrowIfNull(group);

        group.MapPost("/price-lists", CreatePriceListAsync)
            .WithName("CreatePriceList")
            .WithSummary("Creates an empty price list for a site (CAT-05).")
            .Produces<PriceListDto>();

        group.MapGet("/price-lists/{priceListId:guid}", GetPriceListAsync)
            .WithName("GetPriceList")
            .WithSummary("Gets a price list and every entry it currently holds.")
            .Produces<PriceListDto>();

        group.MapGet("/sites/{siteId:guid}/price-lists", GetPriceListsForSiteAsync)
            .WithName("GetPriceListsForSite")
            .WithSummary("Lists every price list for a site.")
            .Produces<List<PriceListDto>>();

        group.MapPost("/price-lists/{priceListId:guid}/entries", AddPriceListEntryAsync)
            .WithName("AddPriceListEntry")
            .WithSummary("Adds a price override for one menu item to a price list. Rejects a second entry for the same item.")
            .Produces<PriceListDto>();

        group.MapGet("/price-lists/{priceListId:guid}/effective-price/{menuItemId:guid}", GetEffectivePriceAsync)
            .WithName("GetEffectivePrice")
            .WithSummary("Resolves the price an item currently charges under a price list — the list's own override, or the item's ordinary price when none is set.")
            .Produces<EffectivePriceDto>();

        return group;
    }

    private static async Task<IResult> CreatePriceListAsync(
        CreatePriceListRequest request,
        CatalogDbContext catalogDb,
        IdentityDbContext identityDb,
        CancellationToken cancellationToken)
    {
        var siteExists = await identityDb.Sites.AnyAsync(s => s.Id == request.SiteId, cancellationToken).ConfigureAwait(false);
        if (!siteExists)
        {
            return Error.NotFound("identity.site_not_found", $"Site {request.SiteId} was not found.").ToProblem();
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            return Error.Validation("catalog.invalid_price_list_name", "Price list name must not be empty.").ToProblem();
        }

        var priceList = new PriceList(request.SiteId, request.Name.Trim());
        catalogDb.PriceLists.Add(priceList);
        await catalogDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(priceList.ToDto());
    }

    private static async Task<IResult> GetPriceListAsync(Guid priceListId, CatalogDbContext catalogDb, CancellationToken cancellationToken)
    {
        var priceList = await FindPriceListAsync(catalogDb, priceListId, cancellationToken).ConfigureAwait(false);
        return priceList is null
            ? PriceListNotFound(priceListId).ToProblem()
            : Results.Ok(priceList.ToDto());
    }

    private static async Task<IResult> GetPriceListsForSiteAsync(
        Guid siteId,
        CatalogDbContext catalogDb,
        IdentityDbContext identityDb,
        CancellationToken cancellationToken)
    {
        var siteExists = await identityDb.Sites.AnyAsync(s => s.Id == siteId, cancellationToken).ConfigureAwait(false);
        if (!siteExists)
        {
            return Error.NotFound("identity.site_not_found", $"Site {siteId} was not found.").ToProblem();
        }

        var priceLists = await catalogDb.PriceLists
            .Include(p => p.Entries)
            .Where(p => p.SiteId == siteId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Sorted client-side: SQLite's EF Core provider (ADR 0012) cannot
        // translate an ORDER BY over DateTimeOffset. Small, tenant-scoped
        // table — client-side ordering costs nothing measurable either way.
        return Results.Ok(priceLists.OrderBy(p => p.CreatedAtUtc).Select(p => p.ToDto()).ToList());
    }

    private static async Task<IResult> AddPriceListEntryAsync(
        Guid priceListId,
        AddPriceListEntryRequest request,
        CatalogDbContext catalogDb,
        CancellationToken cancellationToken)
    {
        var priceList = await FindPriceListAsync(catalogDb, priceListId, cancellationToken).ConfigureAwait(false);
        if (priceList is null)
        {
            return PriceListNotFound(priceListId).ToProblem();
        }

        var menuItemExists = await catalogDb.Items.AnyAsync(i => i.Id == request.MenuItemId, cancellationToken).ConfigureAwait(false);
        if (!menuItemExists)
        {
            return Error.NotFound("catalog.item_not_found", $"Menu item {request.MenuItemId} was not found.").ToProblem();
        }

        var entryResult = priceList.AddEntry(request.MenuItemId, Money.FromDecimal(request.Price));
        if (entryResult.IsFailure)
        {
            return entryResult.Error.ToProblem();
        }

        await catalogDb.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Results.Ok(priceList.ToDto());
    }

    private static async Task<IResult> GetEffectivePriceAsync(
        Guid priceListId,
        Guid menuItemId,
        CatalogDbContext catalogDb,
        CancellationToken cancellationToken)
    {
        var priceList = await FindPriceListAsync(catalogDb, priceListId, cancellationToken).ConfigureAwait(false);
        if (priceList is null)
        {
            return PriceListNotFound(priceListId).ToProblem();
        }

        var override_ = priceList.Entries.FirstOrDefault(e => e.MenuItemId == menuItemId);
        if (override_ is not null)
        {
            return Results.Ok(new EffectivePriceDto(menuItemId, override_.Price.ToDto(), IsOverridden: true));
        }

        var menuItem = await catalogDb.Items.FirstOrDefaultAsync(i => i.Id == menuItemId, cancellationToken).ConfigureAwait(false);
        if (menuItem is null)
        {
            return Error.NotFound("catalog.item_not_found", $"Menu item {menuItemId} was not found.").ToProblem();
        }

        return Results.Ok(new EffectivePriceDto(menuItemId, menuItem.Price.ToDto(), IsOverridden: false));
    }

    private static Task<PriceList?> FindPriceListAsync(CatalogDbContext db, Guid priceListId, CancellationToken cancellationToken)
        => db.PriceLists.Include(p => p.Entries).FirstOrDefaultAsync(p => p.Id == priceListId, cancellationToken);

    private static Error PriceListNotFound(Guid priceListId)
        => Error.NotFound("catalog.price_list_not_found", $"Price list {priceListId} was not found.");
}
