using Brasa.Shared.Persistence;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>A grouping of menu items, e.g. "Entradas" or "Sobremesas".</summary>
public sealed class MenuCategory : Entity
{
    private MenuCategory()
    {
        // EF Core materialisation.
        Name = string.Empty;
    }

    /// <summary>Creates a new category.</summary>
    public MenuCategory(string name, int displayOrder)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Category name must not be empty.", nameof(name));
        }

        Name = name;
        DisplayOrder = displayOrder;
    }

    /// <summary>Display name, e.g. "Entradas".</summary>
    public string Name { get; private set; }

    /// <summary>Sort position within the menu. Lower shows first.</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Whether guests currently see this category.</summary>
    public bool IsVisible { get; private set; } = true;
}
