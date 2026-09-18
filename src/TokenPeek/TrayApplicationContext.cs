using TokenPeek.Providers;

namespace TokenPeek;

/// <summary>
/// WinForms <see cref="ApplicationContext"/> that owns both tray icons, the poll
/// timer, and the hover popup.  No main window is shown; the process runs entirely
/// in the notification area.
///
/// Session icon: blue family.
/// Weekly  icon: green family.
/// Both icons share one right-click context menu.
/// </summary>
public sealed class TrayApplicationContext : ApplicationContext
{
    // ── Dependencies ──────────────────────────────────────────────────────────
    private readonly AppConfig      _config;
    private readonly IUsageProvider _provider;

    // Captured on the UI thread in the constructor; used to marshal back from
    // background tasks without needing a visible window.
    private readonly SynchronizationContext _ui;

    // ── UI objects ────────────────────────────────────────────────────────────
    private readonly NotifyIcon       _sessionIcon;
    private readonly NotifyIcon       _weeklyIcon;
    private readonly HoverPopupForm   _popup;
    private readonly ContextMenuStrip _menu;

    // Menu items we need to update at runtime.
    private readonly ToolStripMenuItem _mnuShowSession;
    private readonly ToolStripMenuItem _mnuShowWeekly;
    private readonly ToolStripMenuItem _mnuStartup;

    // ── State ─────────────────────────────────────────────────────────────────
    private UsageSnapshot? _lastSnapshot;
    private Icon?          _sessionIconHandle;
    private Icon?          _weeklyIconHandle;

    // ── Polling ───────────────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _pollTimer;
    private const int PollIntervalMs = 60_000;
    private bool _refreshing;

    public TrayApplicationContext(AppConfig config, IUsageProvider provider)
    {
        _config   = config;
        _provider = provider;

        // Must be captured here — this constructor runs on the UI thread.
        _ui = SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

        _popup = new HoverPopupForm();

        // Build context menu (shared by both icons).
        _mnuShowSession = new ToolStripMenuItem("Show session",      null, OnToggleSession) { Checked = _config.ShowSession };
        _mnuShowWeekly  = new ToolStripMenuItem("Show weekly",       null, OnToggleWeekly)  { Checked = _config.ShowWeekly  };
        _mnuStartup     = new ToolStripMenuItem("Start with Windows",null, OnToggleStartup) { Checked = _config.StartWithWindows };

        _menu = new ContextMenuStrip();
        _menu.Items.AddRange([
            _mnuShowSession,
            _mnuShowWeekly,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Open Ollama settings", null, (_, _) => OpenUrl("https://ollama.com/settings")),
            new ToolStripSeparator(),
            new ToolStripMenuItem("Refresh",  null, (_, _) => _ = RefreshAsync()),
            new ToolStripMenuItem("API key…", null, (_, _) => OpenSettingsOnUiThread()),
            _mnuStartup,
            new ToolStripSeparator(),
            new ToolStripMenuItem("Exit", null, (_, _) => ExitApp()),
        ]);

        // Session icon (blue).
        _sessionIcon = new NotifyIcon
        {
            Text             = "Token Peek — Session",
            ContextMenuStrip = _menu,
            Visible          = _config.ShowSession,
        };
        _sessionIcon.MouseClick += OnIconMouseClick;
        _sessionIcon.MouseMove  += OnIconMouseMove;

        // Weekly icon (green).
        _weeklyIcon = new NotifyIcon
        {
            Text             = "Token Peek — Weekly",
            ContextMenuStrip = _menu,
            Visible          = _config.ShowWeekly,
        };
        _weeklyIcon.MouseClick += OnIconMouseClick;
        _weeklyIcon.MouseMove  += OnIconMouseMove;

        // Poll timer.
        _pollTimer = new System.Windows.Forms.Timer { Interval = PollIntervalMs };
        _pollTimer.Tick += (_, _) => _ = RefreshAsync();

        // Placeholder icons so the tray slots appear immediately.
        SetPlaceholderIcons();

        // First fetch: small delay so the message pump is running.
        _pollTimer.Start(); // also starts 60s cadence
        _ = DelayedFirstRefreshAsync();

        // If no API key configured, open settings after a short delay so the
        // tray is visible first (gives context to the user).
        if (string.IsNullOrWhiteSpace(_config.GetApiKey()))
        {
            var startupTimer = new System.Windows.Forms.Timer { Interval = 600 };
            startupTimer.Tick += (_, _) =>
            {
                startupTimer.Stop();
                startupTimer.Dispose();
                OpenSettingsOnUiThread();
            };
            startupTimer.Start();
        }
    }

    // ── Mouse handlers ────────────────────────────────────────────────────────

    private void OnIconMouseClick(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Left)
            OpenUrl("https://ollama.com/settings");
    }

    private void OnIconMouseMove(object? sender, MouseEventArgs e)
    {
        if (_lastSnapshot is null) return;
        Point cursor = Control.MousePosition;
        _popup.ShowFor(_lastSnapshot, cursor);
    }

    // ── Menu handlers ─────────────────────────────────────────────────────────

    private void OnToggleSession(object? sender, EventArgs e)
    {
        _config.ShowSession     = !_config.ShowSession;
        _mnuShowSession.Checked = _config.ShowSession;
        _sessionIcon.Visible    = _config.ShowSession;
    }

    private void OnToggleWeekly(object? sender, EventArgs e)
    {
        _config.ShowWeekly     = !_config.ShowWeekly;
        _mnuShowWeekly.Checked = _config.ShowWeekly;
        _weeklyIcon.Visible    = _config.ShowWeekly;
    }

    private void OnToggleStartup(object? sender, EventArgs e)
    {
        _config.StartWithWindows = !_config.StartWithWindows;
        _mnuStartup.Checked      = _config.StartWithWindows;
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    private async Task DelayedFirstRefreshAsync()
    {
        await Task.Delay(500).ConfigureAwait(false);
        await RefreshAsync().ConfigureAwait(false);
    }

    private async Task RefreshAsync()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            var snapshot = await _provider.GetUsageAsync().ConfigureAwait(false);
            _lastSnapshot = snapshot;
            // Marshal icon updates to the UI thread.
            _ui.Post(_ => ApplySnapshot(snapshot), null);
        }
        catch (UnauthorizedAccessException)
        {
            _ui.Post(_ => OpenSettingsOnUiThread(), null);
        }
        catch (Exception ex)
        {
            string msg = TruncTooltip($"Token Peek — error: {ex.Message}");
            _ui.Post(_ =>
            {
                _sessionIcon.Text = msg;
                _weeklyIcon.Text  = msg;
            }, null);
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void ApplySnapshot(UsageSnapshot snap)
    {
        // Session icon.
        var newSession = TrayIconRenderer.Create(snap.Session.Usage, isSession: true);
        var oldSession = _sessionIconHandle;
        _sessionIcon.Icon  = newSession;
        _sessionIconHandle = newSession;
        _sessionIcon.Text  = TruncTooltip($"Session: {snap.Session.UsedPercent}% used");
        oldSession?.Dispose();

        // Weekly icon.
        var newWeekly = TrayIconRenderer.Create(snap.Weekly.Usage, isSession: false);
        var oldWeekly = _weeklyIconHandle;
        _weeklyIcon.Icon  = newWeekly;
        _weeklyIconHandle = newWeekly;
        _weeklyIcon.Text  = TruncTooltip($"Weekly: {snap.Weekly.UsedPercent}% used");
        oldWeekly?.Dispose();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetPlaceholderIcons()
    {
        var s = TrayIconRenderer.Create(1.0, isSession: true);
        _sessionIcon.Icon  = s;
        _sessionIconHandle = s;

        var w = TrayIconRenderer.Create(1.0, isSession: false);
        _weeklyIcon.Icon  = w;
        _weeklyIconHandle = w;
    }

    private static string TruncTooltip(string s) =>
        s.Length > 127 ? s[..127] : s;

    private static void OpenUrl(string url)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { /* ignore */ }
    }

    /// <summary>Always called on the UI thread.</summary>
    private void OpenSettingsOnUiThread()
    {
        // Don't open a second settings window if one is already open.
        foreach (Form f in Application.OpenForms)
        {
            if (f is SettingsForm) { f.Activate(); return; }
        }
        var form = new SettingsForm(_config, () => _ = RefreshAsync());
        form.Show();
    }

    private void ExitApp()
    {
        _popup.HidePopup();
        _pollTimer.Stop();
        _sessionIcon.Visible = false;
        _weeklyIcon.Visible  = false;
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pollTimer.Dispose();
            _sessionIconHandle?.Dispose();
            _weeklyIconHandle?.Dispose();
            _sessionIcon.Dispose();
            _weeklyIcon.Dispose();
            _popup.Dispose();
            _menu.Dispose();
        }
        base.Dispose(disposing);
    }
}
