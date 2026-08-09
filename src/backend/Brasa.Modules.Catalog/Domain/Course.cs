namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// Which point in the meal a <see cref="MenuItem"/> is normally served at
/// (CAT-14) — independent of <see cref="MenuCategory"/>, which is how the
/// menu is organised for browsing, not when a dish is fired to the kitchen.
/// A restaurant can (and often does) group its menu by ingredient or style
/// while every item still belongs to exactly one course. Course *firing*
/// (ORD-07) isn't built yet; this is the tag it will read from once it is —
/// see the "ship the seam ahead of the trigger" pattern this codebase
/// already uses for CAT-13/19.
/// </summary>
public enum Course
{
    Starter = 0,

    Main = 1,

    Dessert = 2,

    /// <summary>Drinks ordered alongside food — not itself fired with a course, but still worth tagging for menu display.</summary>
    Drink = 3,
}
