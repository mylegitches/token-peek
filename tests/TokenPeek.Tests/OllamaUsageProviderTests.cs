using TokenPeek.Providers;

namespace TokenPeek.Tests;

/// <summary>
/// Tests for <see cref="OllamaUsageProvider.Parse"/> using the real JSON shape
/// confirmed from the ollama.com/api/usage endpoint (mid-2026 schema).
/// </summary>
public class OllamaUsageProviderTests
{
    private const string FullJson = """
        {
          "activity": {
            "cost": "0.00000",
            "period": {
              "type": "last_4_weeks",
              "starting_at": "2026-08-21T00:00:00Z",
              "ending_at": "2026-09-18T18:37:00Z"
            },
            "models": []
          },
          "limits": {
            "session": {
              "usage": 0.105,
              "models": [
                { "name": "minimax-m2.7", "request_count": 220 },
                { "name": "gemma4:31b",   "request_count": 16  }
              ]
            },
            "weekly": {
              "usage": 0.144,
              "models": [
                { "name": "gemma4:31b",   "request_count": 1625 },
                { "name": "minimax-m2.7", "request_count": 1339 },
                { "name": "minimax-m3",   "request_count": 126  },
                { "name": "kimi-k2.6",   "request_count": 1    },
                { "name": "glm-5.2",     "request_count": 1    }
              ]
            }
          }
        }
        """;

    [Fact]
    public void Parse_SessionUsageFraction_IsCorrect()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        Assert.Equal(0.105, snap.Session.Usage, precision: 6);
    }

    [Fact]
    public void Parse_SessionRemainingPercent_Is90()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        // (1 - 0.105) * 100 = 89.5 → rounds to 90
        Assert.Equal(90, snap.Session.RemainingPercent);
    }

    [Fact]
    public void Parse_WeeklyUsageFraction_IsCorrect()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        Assert.Equal(0.144, snap.Weekly.Usage, precision: 6);
    }

    [Fact]
    public void Parse_WeeklyRemainingPercent_Is86()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        // (1 - 0.144) * 100 = 85.6 → rounds to 86
        Assert.Equal(86, snap.Weekly.RemainingPercent);
    }

    [Fact]
    public void Parse_SessionModels_OrderedByRequestCountDescending()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        // minimax-m2.7 (220) should be first
        Assert.Equal("minimax-m2.7", snap.Session.Models[0].Name);
        Assert.Equal(220,            snap.Session.Models[0].RequestCount);
        Assert.Equal("gemma4:31b",   snap.Session.Models[1].Name);
        Assert.Equal(16,             snap.Session.Models[1].RequestCount);
    }

    [Fact]
    public void Parse_WeeklyModels_AllFivePresent()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        Assert.Equal(5, snap.Weekly.Models.Count);
    }

    [Fact]
    public void Parse_WeeklyModels_FirstIsGemma_LargestCount()
    {
        var snap = OllamaUsageProvider.Parse(FullJson);
        Assert.Equal("gemma4:31b", snap.Weekly.Models[0].Name);
        Assert.Equal(1625,         snap.Weekly.Models[0].RequestCount);
    }

    [Fact]
    public void Parse_UsageClampedToZeroWhenMissing()
    {
        const string minimal = """{ "limits": { "session": {}, "weekly": {} } }""";
        var snap = OllamaUsageProvider.Parse(minimal);
        Assert.Equal(0.0, snap.Session.Usage);
        Assert.Equal(0.0, snap.Weekly.Usage);
        Assert.Equal(100, snap.Session.RemainingPercent);
        Assert.Equal(100, snap.Weekly.RemainingPercent);
    }

    [Fact]
    public void Parse_UsageOverOneClampedToOne()
    {
        const string bad = """{ "limits": { "session": { "usage": 1.5 }, "weekly": { "usage": 0.5 } } }""";
        var snap = OllamaUsageProvider.Parse(bad);
        Assert.Equal(0, snap.Session.RemainingPercent); // 1 - clamp(1.5,0,1) = 0
    }
}
