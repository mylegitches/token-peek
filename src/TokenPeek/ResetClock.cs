namespace TokenPeek;

/// <summary>
/// Computes and formats Ollama Cloud reset timestamps.
///
/// Ollama resets are on fixed global UTC clocks, not per-user:
///   Session: every 5 hours from the Unix epoch (every multiple of 18,000 s).
///   Weekly:  every Monday at 00:00:00 UTC.
///
/// Neither value is returned by <c>/api/usage</c>; they are derived here.
/// </summary>
public static class ResetClock
{
    private const long SessionPeriodSeconds = 5L * 60 * 60; // 18 000
    private const long WeekPeriodSeconds    = 7L * 24 * 60 * 60; // 604 800

    // 1970-01-01 was a Thursday; 1970-01-05 (Monday) = +4 days = 345 600 s
    private const long MondayOffsetSeconds = 4L * 24 * 60 * 60;

    // ── Public API ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the next session reset boundary strictly after <paramref name="now"/> UTC.
    /// Session windows are 5-hour blocks aligned to the Unix epoch.
    /// </summary>
    public static DateTimeOffset NextSessionReset(DateTimeOffset now)
    {
        long unix = now.ToUnixTimeSeconds();
        long remainder = unix % SessionPeriodSeconds;
        // If we're exactly on a boundary, still advance to the next one.
        long next = unix - remainder + SessionPeriodSeconds;
        return DateTimeOffset.FromUnixTimeSeconds(next);
    }

    /// <summary>
    /// Returns the next weekly reset boundary strictly after <paramref name="now"/> UTC.
    /// Weekly resets happen at Monday 00:00:00 UTC.
    /// </summary>
    public static DateTimeOffset NextWeeklyReset(DateTimeOffset now)
    {
        long unix = now.ToUnixTimeSeconds();
        // Shift so that the Monday anchor (1970-01-05T00:00Z) lines up with 0.
        long shifted = unix - MondayOffsetSeconds;
        long remainder = ((shifted % WeekPeriodSeconds) + WeekPeriodSeconds) % WeekPeriodSeconds;
        long next = unix - remainder + WeekPeriodSeconds;
        return DateTimeOffset.FromUnixTimeSeconds(next);
    }

    /// <summary>
    /// Formats a UTC reset instant in the user's local timezone using the pattern:
    /// <c>Reset: {day}, {MM}/{DD} @ {12H} {tz}.</c>
    ///
    /// Example: <c>Reset: Fri, 09/18 @ 2:00 PM CDT.</c>
    /// </summary>
    /// <param name="utcReset">The UTC reset instant (from <see cref="NextSessionReset"/> or <see cref="NextWeeklyReset"/>).</param>
    /// <param name="tz">Timezone to convert into, typically <see cref="TimeZoneInfo.Local"/>.</param>
    public static string FormatReset(DateTimeOffset utcReset, TimeZoneInfo tz)
    {
        DateTimeOffset local = TimeZoneInfo.ConvertTime(utcReset, tz);

        string day   = local.ToString("ddd"); // "Fri"
        string mmdd  = local.ToString("MM/dd"); // "09/18"
        string time  = local.ToString("h:mm tt"); // "2:00 PM"
        string abbr  = GetTzAbbreviation(local.DateTime, tz);

        return $"Reset: {day}, {mmdd} @ {time} {abbr}.";
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Derives a short timezone abbreviation (e.g. "CDT", "EST") from the Windows
    /// timezone name by taking the first letter of each word.
    /// </summary>
    internal static string GetTzAbbreviation(DateTime localDateTime, TimeZoneInfo tz)
    {
        bool isDst = tz.IsDaylightSavingTime(localDateTime);
        string fullName = isDst ? tz.DaylightName : tz.StandardName;

        // "Central Daylight Time" → "CDT"
        // "UTC" → "UTC"
        char[] initials = fullName
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(w => char.ToUpperInvariant(w[0]))
            .ToArray();

        return new string(initials);
    }
}
