using Brasa.Modules.Fiscal;
using Brasa.Modules.Ordering.Domain;

namespace Brasa.Api.Contracts;

/// <summary>Request body to open a table.</summary>
public sealed record OpenOrderRequest(string TableLabel, int CoverCount);

/// <summary>Request body to ring up a menu item onto an open order.</summary>
public sealed record AddLineRequest(Guid MenuItemId, int Quantity);

/// <summary>One line of an order, as returned to clients.</summary>
public sealed record OrderLineDto(Guid Id, Guid MenuItemId, string ItemName, MoneyDto UnitPrice, int Quantity, MoneyDto LineTotal);

/// <summary>An order, as returned to clients.</summary>
public sealed record OrderDto(
    Guid Id,
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
        order.TableLabel,
        order.CoverCount,
        order.Status.ToString(),
        order.Total.ToDto(),
        [.. order.Lines.Select(l => l.ToDto())]);

    private static OrderLineDto ToDto(this OrderLine line) =>
        new(line.Id, line.MenuItemId, line.ItemName, line.UnitPrice.ToDto(), line.Quantity, line.LineTotal.ToDto());

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
