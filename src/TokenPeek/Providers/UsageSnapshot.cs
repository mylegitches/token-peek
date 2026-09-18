namespace TokenPeek.Providers;

/// <summary>One model's request count within a usage window.</summary>
public record ModelUsage(string Name, int RequestCount);

/// <summary>Raw limit data for one window (session or weekly).</summary>
/// <param name="Usage">Fraction consumed (0–1).</param>
/// <param name="Models">Per-model request counts, ordered by count descending.</param>
public record LimitData(double Usage, IReadOnlyList<ModelUsage> Models)
{
    /// <summary>Fraction remaining (0–1), clamped to [0, 1].</summary>
    public double Remaining => Math.Clamp(1.0 - Usage, 0.0, 1.0);

    /// <summary>Remaining as an integer percentage (0–100).</summary>
    public int RemainingPercent => (int)Math.Round(Remaining * 100);

    /// <summary>Used as an integer percentage (0–100).</summary>
    public int UsedPercent => (int)Math.Round(Math.Clamp(Usage, 0.0, 1.0) * 100);
}

/// <summary>Snapshot from one successful call to <c>GET /api/usage</c>.</summary>
/// <param name="Session">Five-hour rolling window.</param>
/// <param name="Weekly">Seven-day rolling window.</param>
/// <param name="FetchedAt">UTC instant this snapshot was retrieved.</param>
public record UsageSnapshot(LimitData Session, LimitData Weekly, DateTimeOffset FetchedAt);
