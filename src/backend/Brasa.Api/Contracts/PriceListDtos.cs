using Brasa.Modules.Catalog.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A per-site price list (CAT-05), with every entry it currently holds.</summary>
public sealed record PriceListDto(Guid Id, Guid SiteId, string Name, IReadOnlyList<PriceListEntryDto> Entries);

/// <summary>One item's overridden price within a price list.</summary>
public sealed record PriceListEntryDto(Guid Id, Guid MenuItemId, MoneyDto Price);

/// <summary>
/// The price an item resolves to at a site right now: the price list's own
/// override when one exists, otherwise the menu item's ordinary
/// <see cref="MenuItemDto.Price"/>. <c>IsOverridden</c> tells a caller which
/// one it got.
/// </summary>
public sealed record EffectivePriceDto(Guid MenuItemId, MoneyDto Price, bool IsOverridden);

/// <summary>Request body to create a price list for a site.</summary>
public sealed record CreatePriceListRequest(Guid SiteId, string Name);

/// <summary>
/// Request body to add a price override to a list. <c>Price</c> is a
/// decimal in major units, matching <see cref="UpdateMenuItemPriceRequest"/>'s
/// own convention.
/// </summary>
public sealed record AddPriceListEntryRequest(Guid MenuItemId, decimal Price);

/// <summary>Maps price list domain entities to wire DTOs.</summary>
public static class PriceListDtoMappings
{
    public static PriceListDto ToDto(this PriceList priceList) => new(
        priceList.Id,
        priceList.SiteId,
        priceList.Name,
        [.. priceList.Entries.Select(e => e.ToDto())]);

    public static PriceListEntryDto ToDto(this PriceListEntry entry) => new(entry.Id, entry.MenuItemId, entry.Price.ToDto());
}
