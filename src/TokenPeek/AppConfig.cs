using System.Diagnostics;
using System.Drawing;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace TokenPeek;

/// <summary>
/// Persists application settings to <c>%AppData%\TokenPeek\config.json</c>.
/// The API key is encrypted with DPAPI (current-user scope) before storage.
/// </summary>
public sealed class AppConfig
{
    private static readonly string ConfigDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TokenPeek");

    private static readonly string ConfigPath = Path.Combine(ConfigDir, "config.json");

    private const string StartupKeyPath  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string StartupValueName = "TokenPeek";

    // ── Fields ─────────────────────────────────────────────────────────────────

    private ConfigData _data = new();

    // ── Properties ─────────────────────────────────────────────────────────────

    public bool ShowSession
    {
        get => _data.ShowSession;
        set { _data.ShowSession = value; Save(); }
    }

    public bool ShowWeekly
    {
        get => _data.ShowWeekly;
        set { _data.ShowWeekly = value; Save(); }
    }

    /// <summary>
    /// Resolves the API key: environment variable first, then encrypted config file.
    /// Returns <c>null</c> if neither source is configured.
    /// </summary>
    public string? GetApiKey()
    {
        string? envKey = Environment.GetEnvironmentVariable("OLLAMA_API_KEY");
        if (!string.IsNullOrWhiteSpace(envKey))
            return envKey;

        if (_data.EncryptedKey is null) return null;

        try
        {
            byte[] cipher = Convert.FromBase64String(_data.EncryptedKey);
            byte[] plain  = ProtectedData.Unprotect(cipher, null, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plain);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Encrypts and persists the API key.</summary>
    public void SetApiKey(string key)
    {
        byte[] plain  = Encoding.UTF8.GetBytes(key);
        byte[] cipher = ProtectedData.Protect(plain, null, DataProtectionScope.CurrentUser);
        _data.EncryptedKey = Convert.ToBase64String(cipher);
        Save();
    }

    /// <summary>
    /// Whether the app is registered in <c>HKCU\...\Run</c> for startup.
    /// Setting this property updates the registry immediately.
    /// </summary>
    public bool StartWithWindows
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, false);
            return key?.GetValue(StartupValueName) is not null;
        }
        set
        {
            using var key = Registry.CurrentUser.OpenSubKey(StartupKeyPath, true)!;
            if (value)
                key.SetValue(StartupValueName, $"\"{ExePath}\"");
            else
                key.DeleteValue(StartupValueName, false);
        }
    }

    private static string ExePath =>
        Process.GetCurrentProcess().MainModule?.FileName ?? AppContext.BaseDirectory;

    // ── Persistence ────────────────────────────────────────────────────────────

    public static AppConfig Load()
    {
        var cfg = new AppConfig();
        try
        {
            if (File.Exists(ConfigPath))
            {
                string json = File.ReadAllText(ConfigPath);
                cfg._data = JsonSerializer.Deserialize<ConfigData>(json) ?? new ConfigData();
            }
        }
        catch
        {
            // Corrupt config — start fresh.
            cfg._data = new ConfigData();
        }
        return cfg;
    }

    private void Save()
    {
        try
        {
            Directory.CreateDirectory(ConfigDir);
            string json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(ConfigPath, json);
        }
        catch
        {
            // Non-fatal: continue without saving.
        }
    }

    // ── Icon colours ────────────────────────────────────────────────────────────

    // Well-known icon keys (provider – window).
    public const string KeyOllamaSession = "Ollama-Session";
    public const string KeyOllamaWeekly  = "Ollama-Weekly";

    private static readonly Dictionary<string, IconColorEntry> DefaultColors = new()
    {
        [KeyOllamaSession] = new("#1E6FC8", "#FFFFFF"),
        [KeyOllamaWeekly]  = new("#1A8A42", "#FFFFFF"),
    };

    /// <summary>
    /// Returns the configured <see cref="IconColorEntry"/> for <paramref name="iconKey"/>,
    /// or the built-in default if the user has not customised it.
    /// </summary>
    public IconColorEntry GetIconColors(string iconKey)
    {
        if (_data.IconColors is not null && _data.IconColors.TryGetValue(iconKey, out var entry))
            return entry;
        return DefaultColors.TryGetValue(iconKey, out var def) ? def : new("#444444", "#FFFFFF");
    }

    /// <summary>Persists a colour override for one icon.</summary>
    public void SetIconColors(string iconKey, IconColorEntry entry)
    {
        _data.IconColors ??= new Dictionary<string, IconColorEntry>();
        _data.IconColors[iconKey] = entry;
        Save();
    }

    /// <summary>Removes a colour override, reverting to the built-in default.</summary>
    public void ResetIconColors(string iconKey)
    {
        if (_data.IconColors?.Remove(iconKey) == true) Save();
    }

    // ── DTO ────────────────────────────────────────────────────────────────────

    private sealed class ConfigData
    {
        public bool   ShowSession  { get; set; } = true;
        public bool   ShowWeekly   { get; set; } = true;
        public string? EncryptedKey { get; set; }
        public Dictionary<string, IconColorEntry>? IconColors { get; set; }
    }
}

// ── Value type ──────────────────────────────────────────────────────────────────

/// <summary>Background / text colour pair stored as CSS hex strings.</summary>
public sealed class IconColorEntry
{
    [JsonConstructor]
    public IconColorEntry(string bg, string text) { Bg = bg; Text = text; }

    public string Bg   { get; set; }
    public string Text { get; set; }

    public Color BgColor   => ParseColor(Bg,   Color.DimGray);
    public Color TextColor => ParseColor(Text, Color.White);

    private static Color ParseColor(string hex, Color fallback)
    {
        try { return System.Drawing.ColorTranslator.FromHtml(hex); }
        catch { return fallback; }
    }
}
