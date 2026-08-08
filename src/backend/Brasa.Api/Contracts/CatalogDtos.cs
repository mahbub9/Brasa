using Brasa.Modules.Catalog.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A menu item as returned to clients.</summary>
public sealed record MenuItemDto(Guid Id, string Name, MoneyDto Price, decimal VatRatePercent, bool IsAlcoholic, bool IsAvailable);

/// <summary>A menu category with its items, as returned to clients.</summary>
public sealed record MenuCategoryDto(Guid Id, string Name, int DisplayOrder, IReadOnlyList<MenuItemDto> Items);

/// <summary>Maps Catalog domain entities to wire DTOs.</summary>
public static class CatalogDtoMappings
{
    /// <summary>Converts a menu item to its wire representation.</summary>
    public static MenuItemDto ToDto(this MenuItem item) =>
        new(item.Id, item.Name, item.Price.ToDto(), item.VatRate.Fraction, item.IsAlcoholic, item.IsAvailable);
}
