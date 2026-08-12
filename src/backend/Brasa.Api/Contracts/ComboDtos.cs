using Brasa.Modules.Catalog.Domain;

namespace Brasa.Api.Contracts;

/// <summary>A fixed-price bundle of menu items (CAT-10), with every component it currently holds.</summary>
public sealed record ComboDto(Guid Id, string Name, MoneyDto Price, IReadOnlyList<ComboComponentDto> Components);

/// <summary>One item within a combo — always exactly one unit.</summary>
public sealed record ComboComponentDto(Guid Id, Guid MenuItemId);

/// <summary>
/// Request body to create an empty combo. <c>Price</c> is a decimal in
/// major units, matching <see cref="UpdateMenuItemPriceRequest"/>'s own
/// convention.
/// </summary>
public sealed record CreateComboRequest(string Name, decimal Price);

/// <summary>Request body to add one component item to a combo.</summary>
public sealed record AddComboComponentRequest(Guid MenuItemId);

/// <summary>Request body to ring a combo up onto an open order (CAT-10).</summary>
public sealed record AddComboLineRequest(Guid ComboId);

/// <summary>Maps combo domain entities to wire DTOs.</summary>
public static class ComboDtoMappings
{
    public static ComboDto ToDto(this Combo combo) => new(
        combo.Id,
        combo.Name,
        combo.Price.ToDto(),
        [.. combo.Components.Select(c => c.ToDto())]);

    public static ComboComponentDto ToDto(this ComboComponent component) => new(component.Id, component.MenuItemId);
}
