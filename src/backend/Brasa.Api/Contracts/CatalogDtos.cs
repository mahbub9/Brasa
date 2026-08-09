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
    string? Description,
    MoneyDto Price,
    decimal VatRatePercent,
    bool IsAlcoholic,
    bool IsAvailable,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<ModifierGroupDto> ModifierGroups);

/// <summary>A menu category with its items, as returned to clients.</summary>
public sealed record MenuCategoryDto(Guid Id, string Name, int DisplayOrder, IReadOnlyList<MenuItemDto> Items);

/// <summary>
/// Request body to set a menu item's description and declared allergens
/// (CAT-02). Replaces the full allergen set — not additive — so correcting
/// a wrongly-declared allergen is one call. <c>Allergens</c> entries are
/// <see cref="Allergen"/> names, e.g. <c>"Gluten"</c>, <c>"Milk"</c>.
/// </summary>
public sealed record UpdateMenuItemDetailsRequest(string? Description, IReadOnlyList<string>? Allergens);

/// <summary>
/// Bulk menu import request (CAT-17). <c>Csv</c> is the raw file content,
/// header row required: <c>CategoryName,Name,Description,Price,VatRate,IsAlcoholic</c>.
/// Creates only — an existing item with the same name is not matched or
/// updated, so importing the same file twice creates duplicates. Excel
/// (.xlsx) is not supported yet; CSV only.
/// </summary>
public sealed record ImportMenuItemsRequest(string Csv);

/// <summary>One row's import failure — 1-indexed against the data rows (row 1 is the first row after the header), matching what a user sees in a spreadsheet.</summary>
public sealed record ImportMenuItemsRowError(int RowNumber, string Message);

/// <summary>
/// Bulk import result. Partial success is normal — rows import
/// independently, so one bad row (an unknown category, an unparsable
/// price) doesn't block the rest.
/// </summary>
public sealed record ImportMenuItemsResponse(int Created, IReadOnlyList<ImportMenuItemsRowError> Errors);

/// <summary>Maps Catalog domain entities to wire DTOs.</summary>
public static class CatalogDtoMappings
{
    /// <summary>Converts a menu item to its wire representation.</summary>
    public static MenuItemDto ToDto(this MenuItem item) => new(
        item.Id,
        item.Name,
        item.Description,
        item.Price.ToDto(),
        item.VatRate.Fraction,
        item.IsAlcoholic,
        item.IsAvailable,
        [.. item.Allergens.Select(a => a.ToString())],
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
