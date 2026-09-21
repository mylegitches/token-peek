namespace TokenPeek;

/// <summary>
/// Lets the user choose a background and text colour for each tray icon counter.
///
/// Layout (one row per icon):
///
///   Icon         Background        Text
///   Session      [■■■■]            [■■■■]
///   Weekly       [■■■■]            [■■■■]
///
///                [Reset defaults]  [Save]  [Cancel]
/// </summary>
public sealed class ColorSettingsForm : Form
{
    private readonly AppConfig _config;
    private readonly Action    _onSaved;

    // Working copies — only committed to config on Save.
    private readonly Dictionary<string, IconColorEntry> _working = new();

    // The icon definitions shown in this form (key, display label).
    private static readonly (string Key, string Label)[] Icons =
    [
        (AppConfig.KeyOllamaSession, "Session"),
        (AppConfig.KeyOllamaWeekly,  "Weekly"),
    ];

    // Swatch buttons keyed by (iconKey, isBg).
    private readonly Dictionary<(string, bool), Button> _swatches = new();

    public ColorSettingsForm(AppConfig config, Action onSaved)
    {
        _config  = config;
        _onSaved = onSaved;

        // Initialise working copies from current config.
        foreach (var (key, _) in Icons)
            _working[key] = config.GetIconColors(key);

        BuildUi();
    }

    private void BuildUi()
    {
        Text            = "Token Peek — Icon Colors";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterScreen;
        ShowInTaskbar   = false;
        ClientSize      = new Size(360, 170);
        BackColor       = Color.White;
        Font            = new Font("Segoe UI", 9f);

        // Column headers
        var hdrIcon = Label("Icon",       new Point(16, 16), bold: true);
        var hdrBg   = Label("Background", new Point(130, 16), bold: true);
        var hdrText = Label("Text",       new Point(240, 16), bold: true);

        int y = 42;
        foreach (var (key, label) in Icons)
        {
            var rowLabel = Label(label, new Point(16, y + 3));
            var bgBtn    = SwatchButton(key, isBg: true,  new Point(130, y));
            var txtBtn   = SwatchButton(key, isBg: false, new Point(240, y));
            Controls.AddRange([rowLabel, bgBtn, txtBtn]);
            y += 34;
        }

        // Buttons row
        var resetBtn = new Button
        {
            Text      = "Reset defaults",
            Left      = 16, Top = y + 10, Width = 100, Height = 26,
            FlatStyle = FlatStyle.Flat,
        };
        resetBtn.Click += OnReset;

        var saveBtn = new Button
        {
            Text      = "Save",
            Left      = 180, Top = y + 10, Width = 70, Height = 26,
            BackColor = Color.FromArgb(0x1E, 0x6F, 0xC8),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
        };
        saveBtn.FlatAppearance.BorderSize = 0;
        saveBtn.Click += OnSave;

        var cancelBtn = new Button
        {
            Text      = "Cancel",
            Left      = 258, Top = y + 10, Width = 70, Height = 26,
            FlatStyle = FlatStyle.Flat,
        };
        cancelBtn.Click += (_, _) => Close();

        Controls.AddRange([hdrIcon, hdrBg, hdrText, resetBtn, saveBtn, cancelBtn]);
        AcceptButton = saveBtn;
        CancelButton = cancelBtn;
    }

    // ── Control builders ─────────────────────────────────────────────────────

    private static Label Label(string text, Point location, bool bold = false) =>
        new()
        {
            Text      = text,
            Location  = location,
            AutoSize  = true,
            Font      = bold
                ? new Font("Segoe UI", 9f, FontStyle.Bold)
                : new Font("Segoe UI", 9f),
        };

    private Button SwatchButton(string key, bool isBg, Point location)
    {
        var btn = new Button
        {
            Location  = location,
            Size      = new Size(80, 26),
            FlatStyle = FlatStyle.Flat,
            Cursor    = Cursors.Hand,
        };
        btn.FlatAppearance.BorderColor = Color.Gray;
        RefreshSwatch(btn, key, isBg);
        btn.Click += (_, _) => PickColor(btn, key, isBg);
        _swatches[(key, isBg)] = btn;
        return btn;
    }

    private void RefreshSwatch(Button btn, string key, bool isBg)
    {
        var entry  = _working[key];
        var bg     = isBg ? entry.BgColor   : entry.TextColor;
        var fg     = isBg ? entry.TextColor : entry.BgColor;
        btn.BackColor = bg;
        btn.ForeColor = fg;
        btn.Text      = ColorTranslator.ToHtml(bg).ToUpperInvariant();
    }

    // ── Event handlers ────────────────────────────────────────────────────────

    private void PickColor(Button swatch, string key, bool isBg)
    {
        var entry   = _working[key];
        var current = isBg ? entry.BgColor : entry.TextColor;

        using var dlg = new ColorDialog
        {
            Color            = current,
            FullOpen         = true,
            AllowFullOpen    = true,
        };

        if (dlg.ShowDialog(this) != DialogResult.OK) return;

        string hex = ColorTranslator.ToHtml(dlg.Color);
        _working[key] = isBg
            ? new IconColorEntry(hex, entry.Text)
            : new IconColorEntry(entry.Bg, hex);

        RefreshSwatch(swatch, key, isBg);
    }

    private void OnReset(object? sender, EventArgs e)
    {
        foreach (var (key, _) in Icons)
        {
            _config.ResetIconColors(key);
            _working[key] = _config.GetIconColors(key); // reads the default
        }
        // Refresh all swatches.
        foreach (var ((key, isBg), btn) in _swatches)
            RefreshSwatch(btn, key, isBg);
    }

    private void OnSave(object? sender, EventArgs e)
    {
        foreach (var (key, entry) in _working)
            _config.SetIconColors(key, entry);
        _onSaved();
        Close();
    }
}
