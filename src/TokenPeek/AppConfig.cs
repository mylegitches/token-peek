using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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

    // ── DTO ────────────────────────────────────────────────────────────────────

    private sealed class ConfigData
    {
        public bool   ShowSession  { get; set; } = true;
        public bool   ShowWeekly   { get; set; } = true;
        public string? EncryptedKey { get; set; }
    }
}
