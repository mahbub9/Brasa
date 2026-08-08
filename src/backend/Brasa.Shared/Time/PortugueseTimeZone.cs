namespace Brasa.Shared.Time;

/// <summary>
/// The three Portuguese timezones. A site's zone is part of its configuration.
/// </summary>
/// <remarks>
/// <para>
/// This is not a detail that can be deferred. The Azores run one hour behind
/// mainland Portugal, and Madeira shares mainland time — so a chain operating in
/// both Lisbon and Ponta Delgada has two different "todays" at any given moment.
/// Getting this wrong misdates the Z report and puts documents in the wrong
/// SAF-T period.
/// </para>
/// <para>
/// All instants are stored in UTC. This type exists only to render and to compute
/// business-day boundaries.
/// </para>
/// </remarks>
public enum PortugueseRegion
{
    /// <summary>Mainland Portugal. IANA <c>Europe/Lisbon</c>. VAT 6 / 13 / 23%.</summary>
    Continental = 0,

    /// <summary>Madeira. IANA <c>Atlantic/Madeira</c>. Same clock as the mainland; different VAT rates.</summary>
    Madeira = 1,

    /// <summary>Azores. IANA <c>Atlantic/Azores</c>. One hour behind the mainland; different VAT rates.</summary>
    Azores = 2,
}

/// <summary>Resolves <see cref="PortugueseRegion"/> to a <see cref="TimeZoneInfo"/>.</summary>
public static class PortugueseTimeZone
{
    /// <summary>
    /// The IANA identifier for a region. .NET resolves IANA ids on Windows as well
    /// as Linux, so the same string works on a developer machine and in the
    /// container.
    /// </summary>
    public static string IanaId(this PortugueseRegion region) => region switch
    {
        PortugueseRegion.Continental => "Europe/Lisbon",
        PortugueseRegion.Madeira => "Atlantic/Madeira",
        PortugueseRegion.Azores => "Atlantic/Azores",
        _ => throw new ArgumentOutOfRangeException(nameof(region), region, "Unknown region."),
    };

    /// <summary>Looks up the <see cref="TimeZoneInfo"/> for a region.</summary>
    public static TimeZoneInfo ToTimeZoneInfo(this PortugueseRegion region)
        => TimeZoneInfo.FindSystemTimeZoneById(region.IanaId());

    /// <summary>Converts a UTC instant to local wall-clock time in the region.</summary>
    public static DateTimeOffset ToLocal(this PortugueseRegion region, DateTimeOffset utc)
        => TimeZoneInfo.ConvertTime(utc, region.ToTimeZoneInfo());

    /// <summary>
    /// The business day an instant falls in, given the hour at which the site's
    /// trading day rolls over.
    /// </summary>
    /// <param name="region">The site's region.</param>
    /// <param name="utc">The instant to classify.</param>
    /// <param name="dayStart">
    /// Local wall-clock time at which a new business day begins. Restaurants serve
    /// past midnight, so a sale rung at 02:00 belongs to the previous trading day.
    /// A typical value is 04:00 or 06:00.
    /// </param>
    /// <remarks>
    /// Used for the Z report and daily close. It is deliberately <b>not</b> used to
    /// date fiscal documents — those carry the real calendar date, because that is
    /// what AT reconciles the SAF-T period against.
    /// </remarks>
    public static DateOnly BusinessDay(this PortugueseRegion region, DateTimeOffset utc, TimeOnly dayStart)
    {
        var local = region.ToLocal(utc);
        var localDate = DateOnly.FromDateTime(local.DateTime);
        var localTime = TimeOnly.FromDateTime(local.DateTime);

        return localTime < dayStart ? localDate.AddDays(-1) : localDate;
    }
}
