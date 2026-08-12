using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// A named set of per-item price overrides for one <c>Site</c> (CAT-05) —
/// e.g. "Preços Verão" charging more at a beachfront location than the
/// chain's default menu prices.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="SiteId"/> is a plain, opaque reference to a
/// <c>Brasa.Modules.Identity</c> <c>Site</c> row — the same pattern
/// <c>Order.TableId</c> already uses for Floor, and for exactly the same
/// reason: Catalog does not reference or query Identity's tables, and
/// resolving whether a site id is real is the API layer's job, not this
/// module's. See <c>docs/architecture/module-boundaries.md</c>.
/// </para>
/// <para>
/// A deliberately narrow first slice, the same shape IDN-01 shipped: create
/// and read only, no rename/delete yet, and nothing in <c>AddLine</c> or
/// <c>pos</c>/<c>admin</c> resolves an effective price through this yet —
/// there is no site-selection concept anywhere in either client today, so
/// wiring this into ordering would have no way to know which site an order
/// belongs to. This ships the pricing model itself, verified at the API
/// level, the same "mechanism before the trigger" shape CAT-14/15 already
/// established for course/station tags.
/// </para>
/// </remarks>
public sealed class PriceList : Entity
{
    private readonly List<PriceListEntry> _entries = [];

    private PriceList()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new, empty price list for a site.</summary>
    public PriceList(Guid siteId, string name)
    {
        if (siteId == Guid.Empty)
        {
            throw new ArgumentException("Site id must not be empty.", nameof(siteId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Price list name must not be empty.", nameof(name));
        }

        SiteId = siteId;
        Name = name.Trim();
    }

    /// <summary>The site this price list applies to.</summary>
    public Guid SiteId { get; private set; }

    /// <summary>Display name, e.g. "Preços Verão".</summary>
    public string Name { get; private set; }

    /// <summary>The per-item price overrides in this list.</summary>
    public IReadOnlyList<PriceListEntry> Entries => _entries;

    /// <summary>
    /// Adds a price override for a menu item. Rejects a negative price and a
    /// second entry for the same item — one price per item per list, so a
    /// correction is "remove, then re-add," not two entries silently
    /// disagreeing about which one wins.
    /// </summary>
    public Result<PriceListEntry> AddEntry(Guid menuItemId, Money price)
    {
        if (price.IsNegative)
        {
            return Result.Failure<PriceListEntry>(Error.Validation("catalog.invalid_price", "Price must not be negative."));
        }

        if (_entries.Any(e => e.MenuItemId == menuItemId))
        {
            return Result.Failure<PriceListEntry>(Error.Conflict(
                "catalog.price_list_entry_exists", $"This price list already has an entry for menu item {menuItemId}."));
        }

        var entry = new PriceListEntry(Id, menuItemId, price);
        _entries.Add(entry);
        return Result.Success(entry);
    }
}
