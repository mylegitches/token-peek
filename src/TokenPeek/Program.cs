using TokenPeek;
using TokenPeek.Providers;

[assembly: System.Runtime.Versioning.SupportedOSPlatform("windows")]

// Single-instance guard via a named mutex.
const string MutexName = "Global\\TokenPeek_SingleInstance";
using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool isFirst);
if (!isFirst)
{
    MessageBox.Show("Token Peek is already running.", "Token Peek",
        MessageBoxButtons.OK, MessageBoxIcon.Information);
    return;
}

Application.EnableVisualStyles();
Application.SetCompatibleTextRenderingDefault(false);
Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);

var config   = AppConfig.Load();
var http     = new HttpClient();
var provider = new OllamaUsageProvider(http, () => config.GetApiKey());
var context  = new TrayApplicationContext(config, provider);

Application.Run(context);

http.Dispose();
