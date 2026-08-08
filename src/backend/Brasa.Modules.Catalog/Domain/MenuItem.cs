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

    /// <summary>
    /// Current selling price, <b>VAT-inclusive</b> — Portuguese consumer pricing
    /// law requires the price shown to a guest to be the final amount they pay.
    /// New order lines snapshot this; past lines never change.
    /// </summary>
    public Money Price { get; private set; }

    /// <summary>Current VAT rate. See <see cref="Domain.VatRate"/> for why this is a placeholder for the I1 <c>TaxRule</c> model.</summary>
    public VatRate VatRate { get; private set; }

    /// <summary>
    /// True for alcoholic drinks, which sit in a different VAT band from food in
    /// Portugal and must be itemised separately on the invoice.
    /// </summary>
    public bool IsAlcoholic { get; private set; }

    /// <summary>Whether the item can currently be ordered.</summary>
    public bool IsAvailable { get; private set; } = true;

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
}
