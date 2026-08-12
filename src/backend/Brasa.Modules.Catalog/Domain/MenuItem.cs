using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// A sellable item on the menu, with the price and VAT rate charged when it is
/// ordered.
/// </summary>
/// <remarks>
/// When an order line is created, it copies <see cref="Price"/> and
/// <see cref="VatRate"/> at that moment — see <c>Brasa.Modules.Ordering</c>.
/// Changing a price here never rewrites history; it only changes what the
/// <i>next</i> order charges.
/// </remarks>
public sealed class MenuItem : Entity, ISoftDeletable
{
    private readonly List<ModifierGroup> _modifierGroups = [];

    private MenuItem()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new menu item.</summary>
    public MenuItem(Guid categoryId, string name, Money price, VatRate vatRate, bool isAlcoholic = false)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Item name must not be empty.", nameof(name));
        }

        if (price.IsNegative)
        {
            throw new ArgumentException("Price must not be negative.", nameof(price));
        }

        CategoryId = categoryId;
        Name = name;
        Price = price;
        VatRate = vatRate;
        IsAlcoholic = isAlcoholic;
    }

    /// <summary>The category this item is listed under.</summary>
    public Guid CategoryId { get; private set; }

    /// <summary>Display name, e.g. "Bacalhau à Brás".</summary>
    public string Name { get; private set; }

    /// <summary>Free-text description shown under the name, e.g. ingredients or preparation. Optional (CAT-02).</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Allergens present, from the fixed EU-regulated set (CAT-02). Empty
    /// when none declared yet — that is a data-entry gap, not "contains
    /// nothing," so a menu screen must not render it as a safety claim.
    /// </summary>
    public IReadOnlyList<Allergen> Allergens { get; private set; } = [];

    /// <summary>
    /// Current selling price, <b>VAT-inclusive</b> — Portuguese consumer pricing
    /// law requires the price shown to a guest to be the final amount they pay.
    /// New order lines snapshot this; past lines never change.
    /// </summary>
    public Money Price { get; private set; }

    /// <summary>Current VAT rate. See <see cref="Domain.VatRate"/> for why this is a placeholder for the I1 <c>TaxRule</c> model.</summary>
    public VatRate VatRate { get; private set; }

    /// <summary>
    /// A separate price for takeaway/counter-sale orders (CAT-06), used
    /// instead of <see cref="Price"/> when the order is takeaway
    /// (<c>Order.IsTakeaway</c>) — e.g. no table service to price in. Null is
    /// a data-entry gap in the same sense as <see cref="Course"/>: it means
    /// "charge the same as dine-in," not "free." VAT rate is unaffected —
    /// this is a price difference only; <see cref="VatRate"/>'s own
    /// per-channel resolution is <c>TaxRule</c> (CAT-07/08), a separate,
    /// not-yet-built concern.
    /// </summary>
    public Money? TakeawayPrice { get; private set; }

    /// <summary>
    /// True for alcoholic drinks, which sit in a different VAT band from food in
    /// Portugal and must be itemised separately on the invoice.
    /// </summary>
    public bool IsAlcoholic { get; private set; }

    /// <summary>Whether the item can currently be ordered.</summary>
    public bool IsAvailable { get; private set; } = true;

    /// <summary>
    /// Which point in the meal this item is normally served at (CAT-14).
    /// Null is a data-entry gap, the same convention as an empty
    /// <see cref="Allergens"/> list — not a claim that the item has no course.
    /// </summary>
    public Course? Course { get; private set; }

    /// <summary>
    /// Which kitchen station prepares this item (CAT-15). Null is a
    /// data-entry gap, same convention as <see cref="Course"/> — not a claim
    /// that the item is prepared nowhere.
    /// </summary>
    public KitchenStation? Station { get; private set; }

    /// <summary>
    /// The recurring day/time window this item is available in (CAT-11) — a
    /// <i>prato do dia</i>. Null means always available, whatever
    /// <see cref="IsAvailable"/> and the menu's other filters already allow;
    /// it is not itself a claim that the item runs every day.
    /// </summary>
    public MenuItemSchedule? Schedule { get; private set; }

    /// <inheritdoc/>
    /// <remarks>
    /// Distinct from <see cref="IsAvailable"/>: 86'ing is "out of stock today,
    /// back tomorrow"; this is "removed from the menu for good," and only
    /// exists at all because <c>OrderLine.MenuItemId</c> (a snapshot reference,
    /// not a live join) may still point at this row from a past order.
    /// </remarks>
    public DateTimeOffset? DeletedAtUtc { get; set; }

    /// <summary>Marks the item unavailable ("86'd") without deleting it.</summary>
    public void MarkUnavailable() => IsAvailable = false;

    /// <summary>Marks the item available again.</summary>
    public void MarkAvailable() => IsAvailable = true;

    /// <summary>
    /// Soft-deletes the item — see <see cref="ISoftDeletable"/>. The row stays
    /// in the database (past order lines still snapshot its name and price
    /// independently) but disappears from the menu and can no longer be ordered.
    /// </summary>
    public void Delete(DateTimeOffset deletedAtUtc) => DeletedAtUtc = deletedAtUtc;

    /// <summary>Changes the price for future order lines. Past lines are unaffected.</summary>
    public void Reprice(Money newPrice)
    {
        if (newPrice.IsNegative)
        {
            throw new ArgumentException("Price must not be negative.", nameof(newPrice));
        }

        Price = newPrice;
    }

    /// <summary>
    /// Sets or clears a separate takeaway price (CAT-06). Null falls back to
    /// <see cref="Price"/> for takeaway orders too.
    /// </summary>
    public Result SetTakeawayPrice(Money? price)
    {
        if (price is { IsNegative: true })
        {
            return Result.Failure(Error.Validation("catalog.invalid_price", "Price must not be negative."));
        }

        TakeawayPrice = price;
        return Result.Success();
    }

    /// <summary>Sets or clears which course this item is served at (CAT-14).</summary>
    public void SetCourse(Course? course) => Course = course;

    /// <summary>Sets or clears which kitchen station prepares this item (CAT-15).</summary>
    public void SetStation(KitchenStation? station) => Station = station;

    /// <summary>
    /// Sets or clears the recurring window this item is available in
    /// (CAT-11). Null means always available.
    /// </summary>
    public void SetSchedule(MenuItemSchedule? schedule) => Schedule = schedule;

    /// <summary>
    /// Whether <see cref="Schedule"/> allows this item to be sold at the
    /// given local day/time — <c>GetMenuAsync</c>'s guest-facing filter, not
    /// <c>AddLine</c>'s: an order already in progress keeps whatever it was
    /// rung up with, the same "ship the filter, not a retroactive block"
    /// shape <see cref="IsAvailable"/> already has.
    /// </summary>
    public bool IsAvailableAt(DayOfWeek dayOfWeek, TimeOnly timeOfDay)
        => Schedule is null || Schedule.IsActiveAt(dayOfWeek, timeOfDay);

    /// <summary>Sets or clears the description (CAT-02). Null/whitespace clears it.</summary>
    public void SetDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>
    /// Replaces the full declared allergen set (CAT-02) — not additive, so a
    /// correction (an allergen wrongly declared) is one call, not a
    /// remove-then-add.
    /// </summary>
    public void SetAllergens(IEnumerable<Allergen> allergens)
    {
        Allergens = allergens.Distinct().ToList();
    }

    /// <summary>Modifier groups a guest can (or must) choose from when ordering this item. CAT-03.</summary>
    public IReadOnlyList<ModifierGroup> ModifierGroups => _modifierGroups;

    /// <summary>Adds a modifier group to this item, e.g. "Tamanho" or "Extras".</summary>
    public ModifierGroup AddModifierGroup(string name, bool isRequired, int minSelect, int maxSelect, int displayOrder)
    {
        var group = new ModifierGroup(Id, name, isRequired, minSelect, maxSelect, displayOrder);
        _modifierGroups.Add(group);
        return group;
    }
}
