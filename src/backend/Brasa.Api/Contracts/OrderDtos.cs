using Brasa.Modules.Fiscal;
using Brasa.Modules.Ordering.Domain;
using Brasa.Shared.Primitives;

namespace Brasa.Api.Contracts;

/// <summary>Request body to open a table. <c>TableId</c> is a Floor module table id — see <c>GET /floor</c>.</summary>
public sealed record OpenOrderRequest(Guid TableId, int CoverCount);

/// <summary>
/// Request body to ring up a menu item onto an open order.
/// <c>SelectedModifierIds</c> — ids of <c>Modifier</c> rows from the item's
/// own modifier groups (see <c>GET /menu</c>); empty when the item has none.
/// </summary>
public sealed record AddLineRequest(Guid MenuItemId, int Quantity, IReadOnlyList<Guid>? SelectedModifierIds = null);

/// <summary>Request body to move an open order to a different table (ORD-12).</summary>
public sealed record TransferOrderRequest(Guid NewTableId);

/// <summary>Request body to set or clear a line's free-text kitchen note (ORD-06). Null/whitespace clears it.</summary>
public sealed record SetLineNotesRequest(string? Notes);

/// <summary>Request body to move a single line onto a different open order (ORD-13).</summary>
public sealed record TransferLineRequest(Guid DestinationOrderId);

/// <summary>Response body after transferring a line — both orders' new state, since both changed.</summary>
public sealed record TransferLineResponse(OrderDto SourceOrder, OrderDto DestinationOrder);

/// <summary>Request body to merge another open order's lines into this one (ORD-14).</summary>
public sealed record MergeOrdersRequest(Guid SecondaryOrderId);

/// <summary>
/// Response body after a merge — the primary order now carries every line,
/// and the secondary order is <c>Merged</c> (empty, no fiscal document).
/// </summary>
public sealed record MergeOrdersResponse(OrderDto PrimaryOrder, OrderDto SecondaryOrder);

/// <summary>One line's quantity assigned to a guest's share (ORD-16). <c>LineId</c> must belong to the order.</summary>
public sealed record SplitByItemAllocationRequest(Guid LineId, int Quantity);

/// <summary>One guest's share — the lines (and how much of each) they're paying for.</summary>
public sealed record SplitByItemGroupRequest(IReadOnlyList<SplitByItemAllocationRequest> Lines);

/// <summary>
/// Request body to preview a bill split by item (ORD-16). Every line's
/// quantity must be allocated across <c>Groups</c> exactly once — none left
/// over, none double-counted. Doesn't change order state, same as the even
/// split (<c>GET /orders/{id}/split?parts=N</c>) — but this needs a
/// structured body, so unlike that one it can't stay a simple <c>GET</c>.
/// </summary>
public sealed record SplitByItemRequest(IReadOnlyList<SplitByItemGroupRequest> Groups);

/// <summary>One allocated portion of a line within a group's share, as returned to clients.</summary>
public sealed record SplitByItemLineDto(Guid LineId, string ItemName, int Quantity, MoneyDto Total);

/// <summary>One guest's computed share.</summary>
public sealed record SplitByItemGroupDto(IReadOnlyList<SplitByItemLineDto> Lines, MoneyDto Total);

/// <summary>Response body for a by-item split preview.</summary>
public sealed record SplitByItemResponse(IReadOnlyList<SplitByItemGroupDto> Groups);

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
    MoneyDto LineTotal,
    string? Notes);

/// <summary>An order, as returned to clients.</summary>
public sealed record OrderDto(
    Guid Id,
    Guid TableId,
    string TableLabel,
    int CoverCount,
    string Status,
    MoneyDto Total,
    IReadOnlyList<OrderLineDto> Lines);

/// <summary>
/// One row of <c>GET /orders</c> — order history/search (ORD-22). Deliberately
/// lighter than <see cref="OrderDto"/>: a list of many orders doesn't need
/// every line's modifiers, just enough to identify and total each one.
/// </summary>
public sealed record OrderSummaryDto(
    Guid Id,
    Guid TableId,
    string TableLabel,
    int CoverCount,
    string Status,
    MoneyDto Total,
    int LineCount,
    DateTimeOffset OpenedAtUtc,
    DateTimeOffset? ClosedAtUtc);

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

/// <summary>One VAT band's subtotal on a pre-bill, e.g. the 13% and 23% lines shown separately.</summary>
public sealed record VatBreakdownDto(decimal VatRateFraction, MoneyDto NetTotal, MoneyDto VatAmount, MoneyDto GrossTotal);

/// <summary>
/// A pre-bill preview handed to a table before payment — a <em>documento não
/// fiscal</em> (ORD-18/19). Deliberately shaped nothing like
/// <see cref="FiscalDocumentDto"/>: no document number, no ATCUD, no QR
/// payload, because none is issued. <see cref="DocumentKind"/> is a fixed
/// discriminator a client can render as a label so staff and guests never
/// mistake this for an invoice. Computing it never calls
/// <c>IFiscalProvider</c> and never advances a fiscal sequence, so requesting
/// it any number of times (a "reprint") reproduces the same figures — the
/// only field that can differ between two calls is <see cref="GeneratedAtUtc"/>.
/// </summary>
public sealed record PreBillDto(
    Guid OrderId,
    string TableLabel,
    int CoverCount,
    IReadOnlyList<OrderLineDto> Lines,
    IReadOnlyList<VatBreakdownDto> VatBreakdown,
    MoneyDto Total,
    DateTimeOffset GeneratedAtUtc,
    string DocumentKind);

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

    /// <summary>Converts an order to its lightweight history/search row. See <see cref="OrderSummaryDto"/>.</summary>
    public static OrderSummaryDto ToSummaryDto(this Order order) => new(
        order.Id,
        order.TableId,
        order.TableLabel,
        order.CoverCount,
        order.Status.ToString(),
        order.Total.ToDto(),
        order.Lines.Count,
        order.OpenedAtUtc,
        order.ClosedAtUtc);

    private static OrderLineDto ToDto(this OrderLine line) => new(
        line.Id,
        line.MenuItemId,
        line.ItemName,
        line.UnitPrice.ToDto(),
        line.Quantity,
        [.. line.Modifiers.Select(m => new OrderLineModifierDto(m.Id, m.Name, m.PriceDelta.ToDto()))],
        line.LineTotal.ToDto(),
        line.Notes);

    /// <summary>Converts an issued fiscal document to its wire representation.</summary>
    public static FiscalDocumentDto ToDto(this FiscalDocument document) => new(
        document.DocumentNumber,
        document.Atcud,
        document.NetTotal.ToDto(),
        document.VatTotal.ToDto(),
        document.GrossTotal.ToDto(),
        document.QrPayload,
        document.IssuedAtUtc);

    /// <summary>
    /// Builds a pre-bill preview from an order's current lines. <paramref name="fiscalLines"/>
    /// reuses <see cref="FiscalDocumentLine"/>'s gross-inclusive net/VAT derivation
    /// purely as a calculator — no call to <c>IFiscalProvider</c> is involved, so
    /// nothing is issued or numbered. See <see cref="PreBillDto"/>.
    /// </summary>
    public static PreBillDto ToPreBillDto(
        this Order order, IReadOnlyList<FiscalDocumentLine> fiscalLines, DateTimeOffset generatedAtUtc)
    {
        var vatBreakdown = fiscalLines
            .GroupBy(l => l.VatRateFraction)
            .OrderBy(g => g.Key)
            .Select(g => new VatBreakdownDto(
                g.Key,
                Money.Sum(g.Select(l => l.NetTotal)).ToDto(),
                Money.Sum(g.Select(l => l.VatAmount)).ToDto(),
                Money.Sum(g.Select(l => l.GrossTotal)).ToDto()))
            .ToArray();

        return new PreBillDto(
            order.Id,
            order.TableLabel,
            order.CoverCount,
            [.. order.Lines.Select(l => l.ToDto())],
            vatBreakdown,
            order.Total.ToDto(),
            generatedAtUtc,
            "documento_nao_fiscal");
    }
}
