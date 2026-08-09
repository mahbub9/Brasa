namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// Which kitchen station prepares a <see cref="MenuItem"/> (CAT-15) —
/// independent of both <see cref="MenuCategory"/> (browsing) and
/// <see cref="Course"/> (when it's served): a starter and a main can both
/// come off the grill, and a dessert and a drink can both come from the
/// bar. Station *routing* — sending a ticket to the right printer or KDS
/// screen — is KIT-06, not built yet; this is the tag it will read from
/// once it is, the same "ship the seam ahead of the trigger" shape as
/// <see cref="Course"/> (CAT-14).
/// </summary>
public enum KitchenStation
{
    Grill = 0,

    Bar = 1,

    /// <summary>Salads, starters and anything else served without further cooking.</summary>
    ColdKitchen = 2,

    Fryer = 3,

    Pastry = 4,
}
