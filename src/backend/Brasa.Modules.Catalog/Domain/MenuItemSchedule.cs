namespace Brasa.Modules.Catalog.Domain;

/// <summary>
/// Which days of the week a <see cref="MenuItemSchedule"/> applies on.
/// </summary>
/// <remarks>
/// A bitmask, not <c>IReadOnlyList&lt;System.DayOfWeek&gt;</c>, so it maps to a
/// single converted column the same well-proven way <see cref="Course"/> and
/// <see cref="KitchenStation"/> already do, rather than nesting a list
/// conversion inside <see cref="MenuItem.Schedule"/>'s complex-type mapping —
/// untested territory in this codebase, and unnecessary when a flags enum
/// says the same thing in one column.
/// </remarks>
[Flags]
public enum ScheduleDays
{
    None = 0,
    Monday = 1,
    Tuesday = 2,
    Wednesday = 4,
    Thursday = 8,
    Friday = 16,
    Saturday = 32,
    Sunday = 64,
}

/// <summary>Converts between <see cref="System.DayOfWeek"/> and <see cref="ScheduleDays"/>.</summary>
public static class ScheduleDaysExtensions
{
    /// <summary>The single flag corresponding to a <see cref="System.DayOfWeek"/> value.</summary>
    public static ScheduleDays ToScheduleDays(this DayOfWeek dayOfWeek) => dayOfWeek switch
    {
        DayOfWeek.Monday => ScheduleDays.Monday,
        DayOfWeek.Tuesday => ScheduleDays.Tuesday,
        DayOfWeek.Wednesday => ScheduleDays.Wednesday,
        DayOfWeek.Thursday => ScheduleDays.Thursday,
        DayOfWeek.Friday => ScheduleDays.Friday,
        DayOfWeek.Saturday => ScheduleDays.Saturday,
        DayOfWeek.Sunday => ScheduleDays.Sunday,
        _ => throw new ArgumentOutOfRangeException(nameof(dayOfWeek), dayOfWeek, "Unknown day of week."),
    };

    /// <summary>Every individual day set in the mask, Monday first.</summary>
    public static IEnumerable<DayOfWeek> ToDayOfWeeks(this ScheduleDays days)
    {
        if (days.HasFlag(ScheduleDays.Monday))
        {
            yield return DayOfWeek.Monday;
        }

        if (days.HasFlag(ScheduleDays.Tuesday))
        {
            yield return DayOfWeek.Tuesday;
        }

        if (days.HasFlag(ScheduleDays.Wednesday))
        {
            yield return DayOfWeek.Wednesday;
        }

        if (days.HasFlag(ScheduleDays.Thursday))
        {
            yield return DayOfWeek.Thursday;
        }

        if (days.HasFlag(ScheduleDays.Friday))
        {
            yield return DayOfWeek.Friday;
        }

        if (days.HasFlag(ScheduleDays.Saturday))
        {
            yield return DayOfWeek.Saturday;
        }

        if (days.HasFlag(ScheduleDays.Sunday))
        {
            yield return DayOfWeek.Sunday;
        }
    }
}

/// <summary>
/// A recurring window in which a <see cref="MenuItem"/> is available — a
/// <i>prato do dia</i> served only certain days and hours (CAT-11).
/// </summary>
/// <remarks>
/// Deliberately does not support an overnight window (e.g. 22:00–02:00):
/// <see cref="EndTime"/> must be strictly after <see cref="StartTime"/>. Real
/// daily specials in practice run within a single service window (lunch or
/// dinner), and supporting wraparound would mean deciding which calendar day
/// a post-midnight instant belongs to — the same class of problem
/// <c>PortugueseTimeZone.BusinessDay</c> exists for, not one this value type
/// should quietly re-solve on its own. Revisit if a real menu needs it.
/// </remarks>
public sealed record MenuItemSchedule
{
    /// <summary>Creates a schedule. Throws if no day is set or the window is empty/backwards.</summary>
    public MenuItemSchedule(ScheduleDays days, TimeOnly startTime, TimeOnly endTime)
    {
        if (days == ScheduleDays.None)
        {
            throw new ArgumentException("At least one day of the week is required.", nameof(days));
        }

        if (startTime >= endTime)
        {
            throw new ArgumentException("Start time must be before end time.", nameof(startTime));
        }

        Days = days;
        StartTime = startTime;
        EndTime = endTime;
    }

    /// <summary>Which days of the week this schedule applies on.</summary>
    public ScheduleDays Days { get; }

    /// <summary>Local wall-clock time the item becomes available, inclusive.</summary>
    public TimeOnly StartTime { get; }

    /// <summary>Local wall-clock time the item stops being available, exclusive.</summary>
    public TimeOnly EndTime { get; }

    /// <summary>Whether this schedule is active at the given local day and time.</summary>
    public bool IsActiveAt(DayOfWeek dayOfWeek, TimeOnly timeOfDay)
        => Days.HasFlag(dayOfWeek.ToScheduleDays()) && timeOfDay >= StartTime && timeOfDay < EndTime;
}
