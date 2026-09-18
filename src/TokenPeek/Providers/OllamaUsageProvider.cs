using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenPeek.Providers;

/// <summary>
/// Fetches Ollama Cloud quota usage via <c>GET https://ollama.com/api/usage</c>.
///
/// Response shape (undocumented but stable as of mid-2026):
/// <code>
/// {
///   "limits": {
///     "session": { "usage": 0.105, "models": [{ "name": "minimax-m2.7", "request_count": 220 }] },
///     "weekly":  { "usage": 0.144, "models": [{ "name": "gemma4:31b",  "request_count": 1625 }] }
///   }
/// }
/// </code>
/// Authentication: <c>Authorization: Bearer &lt;OLLAMA_API_KEY&gt;</c>
/// </summary>
public sealed class OllamaUsageProvider : IUsageProvider
{
    private const string UsageEndpoint = "https://ollama.com/api/usage";

    private readonly HttpClient _http;
    private readonly Func<string?> _apiKeyResolver;

    /// <param name="httpClient">Caller-managed <see cref="HttpClient"/> (injected for testability).</param>
    /// <param name="apiKeyResolver">Returns the current API key, or <c>null</c> if not configured.</param>
    public OllamaUsageProvider(HttpClient httpClient, Func<string?> apiKeyResolver)
    {
        _http = httpClient;
        _apiKeyResolver = apiKeyResolver;
    }

    /// <inheritdoc/>
    public async Task<UsageSnapshot> GetUsageAsync(CancellationToken ct = default)
    {
        string? key = _apiKeyResolver();
        if (string.IsNullOrWhiteSpace(key))
            throw new UnauthorizedAccessException("Ollama API key is not configured.");

        using var request = new HttpRequestMessage(HttpMethod.Get, UsageEndpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);

        HttpResponseMessage response = await _http.SendAsync(request, ct).ConfigureAwait(false);

        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            throw new UnauthorizedAccessException("Ollama API key was rejected (HTTP 401).");

        response.EnsureSuccessStatusCode();

        string json = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        return Parse(json);
    }

    internal static UsageSnapshot Parse(string json)
    {
        var root = JsonSerializer.Deserialize<OllamaUsageRoot>(json, JsonOptions)
                   ?? throw new JsonException("Empty response from Ollama usage API.");

        LimitData session = Map(root.Limits?.Session);
        LimitData weekly = Map(root.Limits?.Weekly);

        return new UsageSnapshot(session, weekly, DateTimeOffset.UtcNow);
    }

    private static LimitData Map(OllamaLimit? limit)
    {
        if (limit is null)
            return new LimitData(0, Array.Empty<ModelUsage>());

        double usage = Math.Clamp(limit.Usage, 0.0, 1.0);
        var models = (limit.Models ?? [])
            .Select(m => new ModelUsage(m.Name ?? "", m.RequestCount))
            .OrderByDescending(m => m.RequestCount)
            .ToList();

        return new LimitData(usage, models);
    }

    // ── JSON DTOs ──────────────────────────────────────────────────────────────

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private sealed class OllamaUsageRoot
    {
        [JsonPropertyName("limits")]
        public OllamaLimits? Limits { get; set; }
    }

    private sealed class OllamaLimits
    {
        [JsonPropertyName("session")]
        public OllamaLimit? Session { get; set; }

        [JsonPropertyName("weekly")]
        public OllamaLimit? Weekly { get; set; }
    }

    private sealed class OllamaLimit
    {
        [JsonPropertyName("usage")]
        public double Usage { get; set; }

        [JsonPropertyName("models")]
        public List<OllamaModelUsage>? Models { get; set; }
    }

    private sealed class OllamaModelUsage
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("request_count")]
        public int RequestCount { get; set; }
    }
}
