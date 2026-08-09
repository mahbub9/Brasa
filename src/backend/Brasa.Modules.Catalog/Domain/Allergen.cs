namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// One of the 14 allergens EU food-information law (Regulation (EU)
/// No 1169/2011, Annex II) requires be disclosed on a menu. Fixed and stable
/// EU-wide taxonomy, unlike <see cref="VatRate"/> — not a Portugal-specific
/// figure awaiting an accountant's confirmation, so this is safe to model as
/// a closed enum.
/// </summary>
public enum Allergen
{
    /// <summary>Cereals containing gluten (wheat, rye, barley, oats, spelt, kamut).</summary>
    Gluten = 0,

    Crustaceans = 1,

    Eggs = 2,

    Fish = 3,

    Peanuts = 4,

    Soybeans = 5,

    /// <summary>Milk, including lactose.</summary>
    Milk = 6,

    /// <summary>Tree nuts (almonds, hazelnuts, walnuts, cashews, pecans, Brazil nuts, pistachios, macadamia).</summary>
    Nuts = 7,

    Celery = 8,

    Mustard = 9,

    /// <summary>Sesame seeds.</summary>
    Sesame = 10,

    /// <summary>Sulphur dioxide and sulphites, above 10mg/kg or 10mg/L.</summary>
    Sulphites = 11,

    Lupin = 12,

    Molluscs = 13,
}
