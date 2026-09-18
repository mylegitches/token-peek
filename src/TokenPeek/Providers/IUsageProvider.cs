namespace TokenPeek.Providers;

/// <summary>
/// Contract for fetching cloud quota usage from an AI provider.
/// Each implementation targets one specific vendor's API.
/// </summary>
public interface IUsageProvider
{
    /// <summary>Fetch the latest usage snapshot.</summary>
    /// <exception cref="HttpRequestException">Network error or non-2xx response.</exception>
    /// <exception cref="UnauthorizedAccessException">API key missing or rejected (HTTP 401).</exception>
    Task<UsageSnapshot> GetUsageAsync(CancellationToken ct = default);
}
