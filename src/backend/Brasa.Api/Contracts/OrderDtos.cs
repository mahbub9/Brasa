using Brasa.Modules.Fiscal;
using Brasa.Modules.Ordering.Domain;

namespace Brasa.Api.Contracts;

/// <summary>Request body to open a table. <c>TableId</c> is a Floor module table id — see <c>GET /floor</c>.</summary>
public sealed record OpenOrderRequest(Guid TableId, int CoverCount);

/// <summary>
/// Request body to ring up a menu item onto an open order.
/// <c>SelectedModifierIds</c> — ids of <c>Modifier</c> rows from the item's
/// own modifier groups (see <c>GET /menu</c>); empty when the item has none.
/// </summary>
public sealed record AddLineRequest(Guid MenuItemId, int Quantity, IReadOnlyList<Guid>? SelectedModifierIds = null);

/// <summary>A modifier selected on an order line, as returned to clients.</summary>
public sealed record OrderLineModifierDto(Guid Id, string Name, MoneyDto PriceDelta);

/// <summary>One line of an order, as returned to clients.</summary>
public sealed record OrderLineDto(
    Guid Id,
    Guid MenuItemId,
    string ItemName,
    MoneyDto UnitPrice,
    int Quantity,
    IReadOnlyList<OrderLineModifierDto> Modifiers,
    MoneyDto LineTotal);

/// <summary>An order, as returned to clients.</summary>
public sealed record OrderDto(
    Guid Id,
    Guid TableId,
    string TableLabel,
    int CoverCount,
    string Status,
    MoneyDto Total,
    IReadOnlyList<OrderLineDto> Lines);

/// <summary>A fiscal document, as returned to clients.</summary>
public sealed record FiscalDocumentDto(
    string DocumentNumber,
    string Atcud,
    MoneyDto NetTotal,
    MoneyDto VatTotal,
    MoneyDto GrossTotal,
    string QrPayload,
    DateTimeOffset IssuedAtUtc);

/// <summary>Response body when closing an order — the final order state plus the document that settled it.</summary>
public sealed record CloseOrderResponse(OrderDto Order, FiscalDocumentDto Document);

/// <summary>Maps Ordering domain entities and fiscal documents to wire DTOs.</summary>
public static class OrderDtoMappings
{
    /// <summary>Converts an order and its lines to their wire representation.</summary>
    public static OrderDto ToDto(this Order order) => new(
        order.Id,
        order.TableId,
        order.TableLabel,
        order.CoverCount,
        order.Status.ToString(),
        order.Total.ToDto(),
        [.. order.Lines.Select(l => l.ToDto())]);

    private static OrderLineDto ToDto(this OrderLine line) => new(
        line.Id,
        line.MenuItemId,
        line.ItemName,
        line.UnitPrice.ToDto(),
        line.Quantity,
        [.. line.Modifiers.Select(m => new OrderLineModifierDto(m.Id, m.Name, m.PriceDelta.ToDto()))],
        line.LineTotal.ToDto());

    /// <summary>Converts an issued fiscal document to its wire representation.</summary>
    public static FiscalDocumentDto ToDto(this FiscalDocument document) => new(
        document.DocumentNumber,
        document.Atcud,
        document.NetTotal.ToDto(),
        document.VatTotal.ToDto(),
        document.GrossTotal.ToDto(),
        document.QrPayload,
        document.IssuedAtUtc);
}
