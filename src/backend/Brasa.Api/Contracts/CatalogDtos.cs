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

/// <summary>
/// A menu item as returned to clients. <c>Course</c>/<c>Station</c> are null
/// when not yet assigned (CAT-14/CAT-15) — a data-entry gap, not a claim the
/// item has no course or is prepared nowhere. <c>Schedule</c> is null when
/// the item is always available (CAT-11), not just on a recurring window.
/// <c>Price</c> is always the item's <i>effective</i> price right now —
/// <see cref="Domain.MenuItem.EffectivePrice"/>, not the raw stored value —
/// so a due <c>ScheduledPrice</c> (CAT-16) is already reflected in it
/// without the caller having to check <c>ScheduledPrice.IsActive</c> first.
/// </summary>
public sealed record MenuItemDto(
    Guid Id,
    string Name,
    string? Description,
    MoneyDto Price,
    MoneyDto? TakeawayPrice,
    decimal VatRatePercent,
    bool IsAlcoholic,
    bool IsAvailable,
    string? Course,
    string? Station,
    MenuItemScheduleDto? Schedule,
    bool IsCouvert,
    ScheduledPriceDto? ScheduledPrice,
    IReadOnlyList<string> Allergens,
    IReadOnlyList<ModifierGroupDto> ModifierGroups);

/// <summary>
/// A price change queued to take effect at a future instant (CAT-16).
/// <c>EffectiveFromUtc</c> is an ISO 8601 instant. <c>IsActive</c> is true
/// once that instant has passed — at which point <see cref="MenuItemDto.Price"/>
/// already equals <c>NewPrice</c>, this is only surfaced so a caller can see
/// *why*, and so `admin` can distinguish "already live" from "still pending."
/// </summary>
public sealed record ScheduledPriceDto(MoneyDto NewPrice, string EffectiveFromUtc, bool IsActive);

/// <summary>
/// The recurring day/time window a menu item is available in (CAT-11) — a
/// <i>prato do dia</i>. <c>DaysOfWeek</c> entries are <see cref="System.DayOfWeek"/>
/// names, Monday first; <c>StartTime</c>/<c>EndTime</c> are local wall-clock
/// <c>"HH:mm"</c>, start inclusive, end exclusive.
/// </summary>
public sealed record MenuItemScheduleDto(IReadOnlyList<string> DaysOfWeek, string StartTime, string EndTime);

/// <summary>A menu category with its items, as returned to clients.</summary>
public sealed record MenuCategoryDto(Guid Id, string Name, int DisplayOrder, IReadOnlyList<MenuItemDto> Items);

/// <summary>
/// A menu category for management tooling — unlike <see cref="MenuCategoryDto"/>
/// (which only ever appears already-filtered to visible categories, so it
/// has nothing to say on the point), this carries <c>IsVisible</c> because
/// <c>GET /menu/all</c> deliberately returns hidden categories and
/// unavailable items too. See that endpoint's own remarks.
/// </summary>
public sealed record AdminMenuCategoryDto(Guid Id, string Name, int DisplayOrder, bool IsVisible, IReadOnlyList<MenuItemDto> Items);

/// <summary>
/// Request body to set a menu item's description and declared allergens
/// (CAT-02). Replaces the full allergen set — not additive — so correcting
/// a wrongly-declared allergen is one call. <c>Allergens</c> entries are
/// <see cref="Allergen"/> names, e.g. <c>"Gluten"</c>, <c>"Milk"</c>.
/// </summary>
public sealed record UpdateMenuItemDetailsRequest(string? Description, IReadOnlyList<string>? Allergens);

/// <summary>Request body to 86 a menu item or bring it back (CAT-13).</summary>
public sealed record UpdateMenuItemAvailabilityRequest(bool IsAvailable);

/// <summary>
/// Request body to set or clear which course a menu item is served at
/// (CAT-14). <c>Course</c> is a <see cref="Domain.Course"/> name, e.g.
/// <c>"Starter"</c>; null clears it back to unassigned.
/// </summary>
public sealed record UpdateMenuItemCourseRequest(string? Course);

/// <summary>
/// Request body to set or clear which kitchen station prepares a menu item
/// (CAT-15). <c>Station</c> is a <see cref="Domain.KitchenStation"/> name,
/// e.g. <c>"Grill"</c>; null clears it back to unassigned.
/// </summary>
public sealed record UpdateMenuItemStationRequest(string? Station);

/// <summary>Request body to mark or unmark a menu item as couvert (CAT-12).</summary>
public sealed record UpdateMenuItemCouvertRequest(bool IsCouvert);

/// <summary>
/// Request body to set or clear a menu item's pending price change (CAT-16).
/// <c>EffectiveFromUtc</c> is an ISO 8601 instant and must be strictly in
/// the future. Both null clears the pending change; setting only one of the
/// two is rejected — a schedule is all-or-nothing, the same convention
/// <see cref="UpdateMenuItemScheduleRequest"/> (CAT-11) already uses.
/// </summary>
public sealed record SetScheduledPriceRequest(decimal? Price, string? EffectiveFromUtc);

/// <summary>
/// Request body to set or clear a menu item's recurring availability window
/// (CAT-11). <c>DaysOfWeek</c> entries are <see cref="System.DayOfWeek"/> names,
/// e.g. <c>"Monday"</c>; <c>StartTime</c>/<c>EndTime</c> are local
/// <c>"HH:mm"</c>. All three null/empty clears the schedule back to "always
/// available." Setting only some of the three is rejected — a schedule is
/// all-or-nothing, not a partial update.
/// </summary>
public sealed record UpdateMenuItemScheduleRequest(IReadOnlyList<string>? DaysOfWeek, string? StartTime, string? EndTime);

/// <summary>
/// Request body to change a menu item's price. A decimal in major units
/// (e.g. <c>9.50</c>), matching the CSV import convention (CAT-17) — not a
/// full <see cref="MoneyDto"/>, since EUR is the only currency this system
/// represents today.
/// </summary>
public sealed record UpdateMenuItemPriceRequest(decimal Price);

/// <summary>
/// Request body to set or clear a menu item's separate takeaway price
/// (CAT-06), used instead of <see cref="MenuItemDto.Price"/> for takeaway
/// orders. Same decimal-major-units convention as
/// <see cref="UpdateMenuItemPriceRequest"/>; null clears it back to "same as
/// dine-in."
/// </summary>
public sealed record UpdateMenuItemTakeawayPriceRequest(decimal? Price);

/// <summary>Request body to hide a menu category from <c>GET /menu</c>, or show it again (CAT-01).</summary>
public sealed record UpdateMenuCategoryVisibilityRequest(bool IsVisible);

/// <summary>
/// Response for the category-visibility endpoint — deliberately lighter
/// than <see cref="MenuCategoryDto"/>, which never carries <c>IsVisible</c>
/// at all: <c>GET /menu</c> only ever returns visible categories, so a
/// category present in that response has nothing to say on the point.
/// </summary>
public sealed record MenuCategoryVisibilityDto(Guid Id, string Name, bool IsVisible);

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
    /// <summary>
    /// Converts a menu item to its wire representation. <paramref name="nowUtc"/>
    /// resolves <see cref="Domain.MenuItem.EffectivePrice"/> for
    /// <see cref="MenuItemDto.Price"/> and <see cref="ScheduledPriceDto.IsActive"/> —
    /// never <c>DateTimeOffset.UtcNow</c> directly, the caller's own
    /// injected <c>IClock</c>.
    /// </summary>
    public static MenuItemDto ToDto(this MenuItem item, DateTimeOffset nowUtc) => new(
        item.Id,
        item.Name,
        item.Description,
        item.EffectivePrice(nowUtc).ToDto(),
        item.TakeawayPrice?.ToDto(),
        item.VatRate.Fraction,
        item.IsAlcoholic,
        item.IsAvailable,
        item.Course?.ToString(),
        item.Station?.ToString(),
        item.Schedule?.ToDto(),
        item.IsCouvert,
        item.ScheduledPrice?.ToDto(nowUtc),
        [.. item.Allergens.Select(a => a.ToString())],
        [.. item.ModifierGroups.OrderBy(g => g.DisplayOrder).Select(g => g.ToDto())]);

    private static ScheduledPriceDto ToDto(this ScheduledPriceChange change, DateTimeOffset nowUtc) => new(
        change.NewPrice.ToDto(),
        change.EffectiveFromUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
        change.IsActiveAt(nowUtc));

    private static MenuItemScheduleDto ToDto(this MenuItemSchedule schedule) => new(
        [.. schedule.Days.ToDayOfWeeks().Select(d => d.ToString())],
        schedule.StartTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
        schedule.EndTime.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));

    private static ModifierGroupDto ToDto(this ModifierGroup group) => new(
        group.Id,
        group.Name,
        group.IsRequired,
        group.MinSelect,
        group.MaxSelect,
        [.. group.Modifiers.OrderBy(m => m.DisplayOrder).Select(m => m.ToDto())]);

    private static ModifierDto ToDto(this Modifier modifier) => new(modifier.Id, modifier.Name, modifier.PriceDelta.ToDto());
}
