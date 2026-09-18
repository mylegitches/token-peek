using TokenPeek;

namespace TokenPeek.Tests;

/// <summary>
/// Tests for <see cref="ResetClock"/> using the reset instants confirmed against
/// the ollama.com/settings dashboard snapshot from the project description:
///
///   Session reset: 2026-09-18T19:00:00Z  (next 5-hour boundary after ~1:37 PM CDT)
///   Weekly  reset: 2026-09-21T00:00:00Z  (next Monday 00:00 UTC)
///
/// All timezone-dependent formatting tests are pinned to "Central Standard Time"
/// (America/Chicago) so results do not depend on the build machine's timezone.
/// </summary>
public class ResetClockTests
{
    // ── Reference data ──────────────────────────────────────────────────────
    // Page was captured at approximately 2026-09-18T18:37:00Z (1:37 PM CDT).

    private static readonly DateTimeOffset CaptureTime =
        new(2026, 9, 18, 18, 37, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpectedSessionReset =
        new(2026, 9, 18, 19, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset ExpectedWeeklyReset =
        new(2026, 9, 21, 0, 0, 0, TimeSpan.Zero);

    // ── Session reset ────────────────────────────────────────────────────────

    [Fact]
    public void NextSessionReset_ReturnsNextFiveHourBoundary()
    {
        DateTimeOffset result = ResetClock.NextSessionReset(CaptureTime);
        Assert.Equal(ExpectedSessionReset, result);
    }

    [Fact]
    public void NextSessionReset_ExactlyOnBoundary_AdvancesToNextPeriod()
    {
        // If we are exactly on the boundary, the next reset is +5 hours.
        DateTimeOffset boundary = ExpectedSessionReset;
        DateTimeOffset result   = ResetClock.NextSessionReset(boundary);
        Assert.Equal(boundary.AddHours(5), result);
    }

    [Fact]
    public void NextSessionReset_OneSecondBeforeBoundary_ReturnsBoundary()
    {
        DateTimeOffset justBefore = ExpectedSessionReset.AddSeconds(-1);
        DateTimeOffset result     = ResetClock.NextSessionReset(justBefore);
        Assert.Equal(ExpectedSessionReset, result);
    }

    [Fact]
    public void NextSessionReset_AlwaysReturnsFiveHourAlignedTimestamp()
    {
        DateTimeOffset result = ResetClock.NextSessionReset(CaptureTime);
        Assert.Equal(0, result.ToUnixTimeSeconds() % (5 * 3600));
    }

    // ── Weekly reset ─────────────────────────────────────────────────────────

    [Fact]
    public void NextWeeklyReset_ReturnsNextMondayMidnightUtc()
    {
        DateTimeOffset result = ResetClock.NextWeeklyReset(CaptureTime);
        Assert.Equal(ExpectedWeeklyReset, result);
        Assert.Equal(DayOfWeek.Monday, result.UtcDateTime.DayOfWeek);
        Assert.Equal(TimeSpan.Zero, result.UtcDateTime.TimeOfDay);
    }

    [Fact]
    public void NextWeeklyReset_ExactlyOnMonday_AdvancesToNextWeek()
    {
        // If we ARE at Monday 00:00 UTC, the next reset is +7 days.
        DateTimeOffset monday = ExpectedWeeklyReset;
        DateTimeOffset result = ResetClock.NextWeeklyReset(monday);
        Assert.Equal(monday.AddDays(7), result);
        Assert.Equal(DayOfWeek.Monday, result.UtcDateTime.DayOfWeek);
    }

    [Fact]
    public void NextWeeklyReset_OneSecondBeforeMonday_ReturnsMonday()
    {
        DateTimeOffset justBefore = ExpectedWeeklyReset.AddSeconds(-1);
        DateTimeOffset result     = ResetClock.NextWeeklyReset(justBefore);
        Assert.Equal(ExpectedWeeklyReset, result);
    }

    // ── Format: timezone abbreviation ────────────────────────────────────────

    [Fact]
    public void GetTzAbbreviation_CentralDaylightTime_ReturnsCDT()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        // Sep 18 is in DST in the US (CDT).
        DateTime local = new(2026, 9, 18, 14, 0, 0); // 2:00 PM local (DST active)
        string abbr = ResetClock.GetTzAbbreviation(local, tz);
        Assert.Equal("CDT", abbr);
    }

    [Fact]
    public void GetTzAbbreviation_CentralStandardTime_ReturnsCST()
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        // January is not in DST (CST).
        DateTime local = new(2026, 1, 5, 10, 0, 0);
        string abbr = ResetClock.GetTzAbbreviation(local, tz);
        Assert.Equal("CST", abbr);
    }

    // ── Format: full string ───────────────────────────────────────────────────

    [Fact]
    public void FormatReset_SessionReset_CentralTime_MatchesDashboardExample()
    {
        // Dashboard shows "2:00 PM" for session reset in CDT.
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        string result = ResetClock.FormatReset(ExpectedSessionReset, tz);
        Assert.Equal("Reset: Fri, 09/18 @ 2:00 PM CDT.", result);
    }

    [Fact]
    public void FormatReset_WeeklyReset_CentralTime_MatchesDashboardExample()
    {
        // Dashboard shows "Sun, Sep 20, 7:00 PM" for the weekly reset in CDT.
        // 2026-09-21T00:00Z = 2026-09-20T19:00 CDT (Sunday evening locally).
        var tz = TimeZoneInfo.FindSystemTimeZoneById("Central Standard Time");
        string result = ResetClock.FormatReset(ExpectedWeeklyReset, tz);
        Assert.Equal("Reset: Sun, 09/20 @ 7:00 PM CDT.", result);
    }
}
