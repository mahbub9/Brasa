namespace Brasa.Shared.Time;

/// <summary>
/// The source of the current time.
/// </summary>
/// <remarks>
/// Never call <see cref="DateTime.UtcNow"/> directly in domain or module code.
/// Fiscal documents carry a <c>SystemEntryDate</c> that must be monotonic within
/// a series, and the signature chain is only testable if time is injectable.
/// Tests substitute a fixed or controllable clock.
/// </remarks>
public interface IClock
{
    /// <summary>The current instant, always in UTC.</summary>
    DateTimeOffset UtcNow { get; }
}

/// <summary>The real system clock. Registered as a singleton.</summary>
public sealed class SystemClock : IClock
{
    /// <inheritdoc/>
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
