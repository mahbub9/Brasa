using Brasa.Shared.Primitives;

namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// A price change queued to take effect at a future instant (CAT-16) — e.g.
/// "€10.50, effective 2026-09-01" set today while the item still charges
/// €9.50 until then.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not driven by a background job — nothing in this codebase
/// runs one yet (Hangfire is OPS-10, not built). Instead this is evaluated
/// lazily, the same way <see cref="MenuItemSchedule"/> (CAT-11) resolves a
/// recurring window on every read rather than flipping a stored flag at the
/// right moment: <see cref="MenuItem.EffectivePrice"/> compares the current
/// instant against <see cref="EffectiveFromUtc"/> every time it is called,
/// so correctness never depends on anything having run recently. The
/// change is never "promoted" into <see cref="MenuItem.Price"/> either —
/// once due, <c>EffectivePrice</c> keeps returning <see cref="NewPrice"/>
/// indefinitely, which is exactly as correct as promoting would be, without
/// a write-during-a-read anti-pattern to reason about.
/// </para>
/// <para>
/// A narrow first slice: only <see cref="MenuItem.Price"/> (the dine-in
/// price) can be scheduled, not <see cref="MenuItem.TakeawayPrice"/>, and
/// only one pending change can exist at a time — setting a new one replaces
/// whatever was pending, it does not queue a second. Scheduling a whole
/// category's visibility, or several changes stacked in sequence, are both
/// real features this does not attempt.
/// </para>
/// </remarks>
public sealed record ScheduledPriceChange
{
    /// <summary>Creates a scheduled price change. "Must be in the future" is checked by the API layer, which has the clock.</summary>
    public ScheduledPriceChange(Money newPrice, DateTimeOffset effectiveFromUtc)
    {
        if (newPrice.IsNegative)
        {
            throw new ArgumentException("Price must not be negative.", nameof(newPrice));
        }

        NewPrice = newPrice;
        EffectiveFromUtc = effectiveFromUtc;
    }

    /// <summary>The price that takes over once <see cref="EffectiveFromUtc"/> has passed.</summary>
    public Money NewPrice { get; }

    /// <summary>The instant, in UTC, at and after which <see cref="NewPrice"/> applies.</summary>
    public DateTimeOffset EffectiveFromUtc { get; }

    /// <summary>Whether this change has taken effect as of <paramref name="nowUtc"/>.</summary>
    public bool IsActiveAt(DateTimeOffset nowUtc) => nowUtc >= EffectiveFromUtc;
}
