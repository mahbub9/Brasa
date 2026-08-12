using Brasa.Shared.Persistence;
using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// A named, fixed-price bundle of specific menu items (CAT-10) — a
/// <i>menu do dia</i>: soup + main + drink for one price lower than the sum
/// of their standalone prices.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately holds no fiscal logic of its own. Portuguese VAT is charged
/// on the underlying goods, not on the bundle wrapper — a combo mixing a
/// 13% main and a 23% drink cannot legally collapse into one line at one
/// rate. Instead, ringing a combo up (the API layer's
/// <c>AddComboLineAsync</c>, composing this module with Ordering) allocates
/// <see cref="Price"/> across <see cref="Components"/> via
/// <c>Money.Allocate</c>, weighted by each component's own current
/// standalone price — the exact same proration ORD-11 already uses to
/// prorate an order-level discount across lines — then adds each component
/// as an ordinary <c>OrderLine</c> at its own real VAT rate. A combo is
/// therefore never a new fiscal concept, only a priced-lower bundle of
/// ordinary ones.
/// </para>
/// <para>
/// A narrow first slice: each component is exactly one unit of a specific
/// item, not a guest's choice among several ("any starter"), and not a
/// quantity greater than one — both are real product features, deferred
/// rather than guessed at. No rename/delete/remove-component yet, the same
/// shape CAT-05/IDN-01 shipped. Nothing in <c>pos</c>/<c>admin</c> offers a
/// "ring up this combo" UI yet; <c>POST /orders/{id}/combo-lines</c> is
/// verified at the API level only, the same "mechanism before the trigger"
/// shape CAT-14/15 already established.
/// </para>
/// </remarks>
public sealed class Combo : Entity
{
    private readonly List<ComboComponent> _components = [];

    private Combo()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new, empty combo.</summary>
    public Combo(string name, Money price)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Combo name must not be empty.", nameof(name));
        }

        if (price.IsNegative)
        {
            throw new ArgumentException("Price must not be negative.", nameof(price));
        }

        Name = name.Trim();
        Price = price;
    }

    /// <summary>Display name, e.g. "Menu do Dia".</summary>
    public string Name { get; private set; }

    /// <summary>
    /// The fixed, VAT-inclusive price a guest pays for the whole bundle —
    /// almost always less than the sum of its components' standalone prices.
    /// </summary>
    public Money Price { get; private set; }

    /// <summary>The specific items that make up this combo, one unit of each.</summary>
    public IReadOnlyList<ComboComponent> Components => _components;

    /// <summary>Adds a component item. Rejects a second entry for the same item.</summary>
    public Result<ComboComponent> AddComponent(Guid menuItemId)
    {
        if (_components.Any(c => c.MenuItemId == menuItemId))
        {
            return Result.Failure<ComboComponent>(Error.Conflict(
                "catalog.combo_component_exists", $"This combo already has a component for menu item {menuItemId}."));
        }

        var component = new ComboComponent(Id, menuItemId);
        _components.Add(component);
        return Result.Success(component);
    }
}
