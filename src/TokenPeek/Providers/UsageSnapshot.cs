namespace TokenPeek.Providers;

/// <summary>One model's request count within a usage window.</summary>
public record ModelUsage(string Name, int RequestCount);

/// <summary>Raw limit data for one window (session, weekly, or monthly).</summary>
/// <param name="Usage">Fraction consumed (0–1).</param>
/// <param name="Models">Per-model request counts, ordered by count descending.</param>
/// <param name="ResetAt">
/// Provider-supplied reset instant (Claude, Codex). <c>null</c> means Token Peek
/// computes the reset via <see cref="ResetClock"/> (Ollama).
/// </param>
public record LimitData(double Usage, IReadOnlyList<ModelUsage> Models, DateTimeOffset? ResetAt = null)
{
    /// <summary>Fraction remaining (0–1), clamped to [0, 1].</summary>
    public double Remaining => Math.Clamp(1.0 - Usage, 0.0, 1.0);

    /// <summary>Remaining as an integer percentage (0–100).</summary>
    public int RemainingPercent => (int)Math.Round(Remaining * 100);

    /// <summary>Used as an integer percentage (0–100).</summary>
    public int UsedPercent => (int)Math.Round(Math.Clamp(Usage, 0.0, 1.0) * 100);
}

/// <summary>
/// Snapshot from one successful provider fetch.
/// <para>
/// <paramref name="Weekly"/> is <c>null</c> for providers with only one billing window (e.g. Cursor monthly).
/// </para>
/// </summary>
public record UsageSnapshot(
    LimitData          Session,
    LimitData?         Weekly,
    DateTimeOffset     FetchedAt,
    string             ProviderName      = "",
    string             SessionWindowName = "Session",
    string             WeeklyWindowName  = "Weekly"
);
