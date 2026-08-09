using Brasa.Shared.Time;

namespace Brasa.Shared.Tests.Time;

/// <summary>
/// CLAUDE.md calls this out directly: "the Azores are an hour behind the
/// mainland, which affects daily close and SAF-T period boundaries." This
/// type had zero test coverage before this file — these pin the two things
/// most likely to be gotten wrong: the IANA ids actually resolve on this
/// machine's OS/ICU, and <see cref="PortugueseTimeZone.BusinessDay"/> can
/// genuinely disagree about "today" between regions for the same instant.
/// </summary>
public sealed class PortugueseTimeZoneTests
{
    [Theory]
    [InlineData(PortugueseRegion.Continental, "Europe/Lisbon")]
    [InlineData(PortugueseRegion.Madeira, "Atlantic/Madeira")]
    [InlineData(PortugueseRegion.Azores, "Atlantic/Azores")]
    public void IanaId_maps_each_region_correctly(PortugueseRegion region, string expectedIanaId)
    {
        region.IanaId().ShouldBe(expectedIanaId);
    }

    [Fact]
    public void IanaId_throws_for_an_undefined_region()
    {
        var undefined = (PortugueseRegion)99;

        Should.Throw<ArgumentOutOfRangeException>(() => undefined.IanaId());
    }

    [Theory]
    [InlineData(PortugueseRegion.Continental)]
    [InlineData(PortugueseRegion.Madeira)]
    [InlineData(PortugueseRegion.Azores)]
    public void Every_region_s_IANA_id_actually_resolves_on_this_runtime(PortugueseRegion region)
    {
        // .NET resolves IANA ids via the OS/ICU. This is the test that would
        // have caught it if that silently didn't work on some deployment
        // target — a TimeZoneNotFoundException here happens at first use in
        // production, not at compile time.
        Should.NotThrow(() => region.ToTimeZoneInfo());
    }

    [Fact]
    public void Azores_local_time_is_one_hour_behind_the_mainland_for_the_same_instant()
    {
        // Well inside the EU DST window (both Continental and Azores observe
        // the same EU DST transition dates, so the one-hour differential
        // holds year-round) and nowhere near a transition boundary, so this
        // is deterministic regardless of when the test runs.
        var utc = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var continentalLocal = PortugueseRegion.Continental.ToLocal(utc);
        var azoresLocal = PortugueseRegion.Azores.ToLocal(utc);

        (continentalLocal.DateTime - azoresLocal.DateTime).ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public void Madeira_shares_the_mainland_s_clock()
    {
        var utc = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.Zero);

        var continentalLocal = PortugueseRegion.Continental.ToLocal(utc);
        var madeiraLocal = PortugueseRegion.Madeira.ToLocal(utc);

        madeiraLocal.DateTime.ShouldBe(continentalLocal.DateTime);
    }

    [Fact]
    public void A_sale_before_the_rollover_hour_belongs_to_the_previous_business_day()
    {
        // Continental local 01:00 on 2026-07-15 (DST, UTC+1) -> 2026-07-15T00:00:00Z.
        var utc = new DateTimeOffset(2026, 7, 15, 0, 0, 0, TimeSpan.Zero);
        var dayStart = new TimeOnly(6, 0);

        PortugueseRegion.Continental.BusinessDay(utc, dayStart).ShouldBe(new DateOnly(2026, 7, 14));
    }

    [Fact]
    public void A_sale_after_the_rollover_hour_belongs_to_the_calendar_day_it_falls_on()
    {
        // Continental local 08:00 on 2026-07-15 (DST, UTC+1) -> 2026-07-15T07:00:00Z.
        var utc = new DateTimeOffset(2026, 7, 15, 7, 0, 0, TimeSpan.Zero);
        var dayStart = new TimeOnly(6, 0);

        PortugueseRegion.Continental.BusinessDay(utc, dayStart).ShouldBe(new DateOnly(2026, 7, 15));
    }

    [Fact]
    public void The_rollover_hour_itself_belongs_to_the_new_business_day_not_the_old_one()
    {
        // Continental local exactly 06:00:00 on 2026-07-15 (DST, UTC+1) -> 2026-07-15T05:00:00Z.
        var utc = new DateTimeOffset(2026, 7, 15, 5, 0, 0, TimeSpan.Zero);
        var dayStart = new TimeOnly(6, 0);

        PortugueseRegion.Continental.BusinessDay(utc, dayStart).ShouldBe(new DateOnly(2026, 7, 15));
    }

    [Fact]
    public void A_sale_just_after_midnight_rolls_back_across_a_month_boundary()
    {
        // Continental local 01:00 on 2026-08-01 (DST, UTC+1) -> 2026-08-01T00:00:00Z.
        var utc = new DateTimeOffset(2026, 8, 1, 0, 0, 0, TimeSpan.Zero);
        var dayStart = new TimeOnly(6, 0);

        PortugueseRegion.Continental.BusinessDay(utc, dayStart).ShouldBe(new DateOnly(2026, 7, 31));
    }

    [Fact]
    public void The_same_instant_can_fall_on_two_different_business_days_in_different_regions()
    {
        // The exact scenario the type's own doc comment warns about: "a chain
        // operating in both Lisbon and Ponta Delgada has two different
        // 'todays' at any given moment." At 2026-07-15T05:30:00Z, Continental
        // local (DST, UTC+1) is 06:30 -- past a 06:00 rollover, so "today".
        // Azores local (DST, UTC+0) is 05:30 at the very same instant --
        // before the rollover, so still "yesterday".
        var utc = new DateTimeOffset(2026, 7, 15, 5, 30, 0, TimeSpan.Zero);
        var dayStart = new TimeOnly(6, 0);

        var continentalDay = PortugueseRegion.Continental.BusinessDay(utc, dayStart);
        var azoresDay = PortugueseRegion.Azores.BusinessDay(utc, dayStart);

        continentalDay.ShouldBe(new DateOnly(2026, 7, 15));
        azoresDay.ShouldBe(new DateOnly(2026, 7, 14));
        continentalDay.ShouldNotBe(azoresDay);
    }
}
