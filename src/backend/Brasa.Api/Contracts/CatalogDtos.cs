using Brasa.Modules.Catalog.Domain;

namespace Brasa.Api.Contracts;

/// <summary>One choice within a <see cref="ModifierGroupDto"/>.</summary>
public sealed record ModifierDto(Guid Id, string Name, MoneyDto PriceDelta);

/// <summary>A group of related modifier choices for a menu item.</summary>
public sealed record ModifierGroupDto(
    Guid Id,
    string Name,
    bool IsRequired,
    int MinSelect,
    int MaxSelect,
    IReadOnlyList<ModifierDto> Modifiers);

/// <summary>A menu item as returned to clients.</summary>
public sealed record MenuItemDto(
    Guid Id,
    string Name,
    MoneyDto Price,
    decimal VatRatePercent,
    bool IsAlcoholic,
    bool IsAvailable,
    IReadOnlyList<ModifierGroupDto> ModifierGroups);

/// <summary>A menu category with its items, as returned to clients.</summary>
public sealed record MenuCategoryDto(Guid Id, string Name, int DisplayOrder, IReadOnlyList<MenuItemDto> Items);

/// <summary>Maps Catalog domain entities to wire DTOs.</summary>
public static class CatalogDtoMappings
{
    /// <summary>Converts a menu item to its wire representation.</summary>
    public static MenuItemDto ToDto(this MenuItem item) => new(
        item.Id,
        item.Name,
        item.Price.ToDto(),
        item.VatRate.Fraction,
        item.IsAlcoholic,
        item.IsAvailable,
        [.. item.ModifierGroups.OrderBy(g => g.DisplayOrder).Select(g => g.ToDto())]);

    private static ModifierGroupDto ToDto(this ModifierGroup group) => new(
        group.Id,
        group.Name,
        group.IsRequired,
        group.MinSelect,
        group.MaxSelect,
        [.. group.Modifiers.OrderBy(m => m.DisplayOrder).Select(m => m.ToDto())]);

    private static ModifierDto ToDto(this Modifier modifier) => new(modifier.Id, modifier.Name, modifier.PriceDelta.ToDto());
}
