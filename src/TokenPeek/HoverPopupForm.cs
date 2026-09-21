using TokenPeek.Providers;

namespace TokenPeek;

/// <summary>
/// Borderless, always-on-top popup shown when the user hovers a tray icon.
///
/// Layout:
///   Session  89% remaining
///   Reset: Fri, 09/18 @ 2:00 PM CDT.
///
///   Weekly   86% remaining
///   Reset: Sun, 09/20 @ 7:00 PM CDT.
///
///   Models this week:
///   gemma4:31b    1,625 req
///   minimax-m2.7  1,339 req
///   …
/// </summary>
public sealed class HoverPopupForm : Form
{
    // ── Colours ──────────────────────────────────────────────────────────────
    private static readonly Color BgColor       = Color.FromArgb(0x1C, 0x1C, 0x1E);
    private static readonly Color TextPrimary   = Color.White;
    private static readonly Color TextSecondary = Color.FromArgb(0xAA, 0xAA, 0xAA);
    private static readonly Color AccentSession = Color.FromArgb(0x4A, 0xA5, 0xFF);
    private static readonly Color AccentWeekly  = Color.FromArgb(0x3D, 0xC4, 0x7A);
    private static readonly Color BorderColor   = Color.FromArgb(0x3A, 0x3A, 0x3C);

    // ── Layout constants ─────────────────────────────────────────────────────
    private const int Pad        = 12;
    private const int PopupWidth = 260;
    private const int MinHeight  = 80;
    private const int InnerWidth = PopupWidth - Pad * 2;

    // ── Controls ─────────────────────────────────────────────────────────────
    private readonly Panel           _panel;
    private readonly Label           _sessionLabel = new();
    private readonly Label           _sessionReset = new();
    private readonly Label           _weeklyLabel  = new();
    private readonly Label           _weeklyReset  = new();
    private readonly Label           _modelsHeader = new();
    private readonly FlowLayoutPanel _modelsPanel  = new();

    // ── Auto-hide timer ───────────────────────────────────────────────────────
    private readonly System.Windows.Forms.Timer _hideTimer;
    private const int HideDelayMs = 2_000;

    public HoverPopupForm()
    {
        Text            = "";
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar   = false;
        TopMost         = true;
        BackColor       = BgColor;
        Padding         = new Padding(0);
        AutoSize        = false;
        this.Width      = PopupWidth;

        _panel = new Panel
        {
            Dock      = DockStyle.Fill,
            BackColor = BgColor,
            Padding   = new Padding(Pad),
        };
        Controls.Add(_panel);

        BuildLayout();

        _hideTimer = new System.Windows.Forms.Timer { Interval = HideDelayMs };
        _hideTimer.Tick += (_, _) => HidePopup();

        Paint += OnPaint;
    }

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>Updates the popup with the latest snapshot and shows it near <paramref name="cursorPos"/>.</summary>
    public void ShowFor(UsageSnapshot snapshot, Point cursorPos)
    {
        Populate(snapshot);
        PositionNear(cursorPos);
        if (!Visible) Show();
        KeepAlive();
    }

    /// <summary>Resets the auto-hide timer (call on each MouseMove).</summary>
    public void KeepAlive()
    {
        _hideTimer.Stop();
        _hideTimer.Start();
    }

    public void HidePopup()
    {
        _hideTimer.Stop();
        if (Visible) Hide();
    }

    // ── Layout ───────────────────────────────────────────────────────────────

    private void BuildLayout()
    {
        _sessionLabel.AutoSize  = true;
        _sessionLabel.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        _sessionLabel.ForeColor = AccentSession;

        _sessionReset.AutoSize  = true;
        _sessionReset.Font      = new Font("Segoe UI", 8f);
        _sessionReset.ForeColor = TextSecondary;

        _weeklyLabel.AutoSize  = true;
        _weeklyLabel.Font      = new Font("Segoe UI", 9f, FontStyle.Bold);
        _weeklyLabel.ForeColor = AccentWeekly;

        _weeklyReset.AutoSize  = true;
        _weeklyReset.Font      = new Font("Segoe UI", 8f);
        _weeklyReset.ForeColor = TextSecondary;

        _modelsHeader.AutoSize  = true;
        _modelsHeader.Font      = new Font("Segoe UI", 7.5f, FontStyle.Bold);
        _modelsHeader.ForeColor = TextSecondary;
        _modelsHeader.Text      = "Models this week:";

        _modelsPanel.AutoSize      = true;
        _modelsPanel.FlowDirection = FlowDirection.TopDown;
        _modelsPanel.WrapContents  = false;
        _modelsPanel.BackColor     = BgColor;
        _modelsPanel.Width         = InnerWidth;

        _panel.Controls.AddRange([
            _sessionLabel,
            _sessionReset,
            MakeSpacer(4),
            _weeklyLabel,
            _weeklyReset,
            MakeSpacer(8),
            _modelsHeader,
            _modelsPanel,
        ]);

        ArrangeControls();
    }

    private void ArrangeControls()
    {
        int y = Pad;
        foreach (Control c in _panel.Controls)
        {
            c.Left = Pad;
            c.Top  = y;
            y += c.Height + 2;
        }
    }

    private void Populate(UsageSnapshot snap)
    {
        var tz  = TimeZoneInfo.Local;
        var now = DateTimeOffset.UtcNow;

        // Session
        _sessionLabel.Text = $"Session  {snap.Session.UsedPercent}% used";
        _sessionReset.Text = ResetClock.FormatReset(ResetClock.NextSessionReset(now), tz);

        // Weekly (may be null for single-window providers like Cursor)
        if (snap.Weekly is { } weekly)
        {
            _weeklyLabel.Text = $"Weekly   {weekly.UsedPercent}% used";
            _weeklyReset.Text = weekly.ResetAt.HasValue
                ? ResetClock.FormatReset(weekly.ResetAt.Value, tz)
                : ResetClock.FormatReset(ResetClock.NextWeeklyReset(now), tz);
        }
        _weeklyLabel.Visible = snap.Weekly is not null;
        _weeklyReset.Visible = snap.Weekly is not null;

        // Models
        _modelsPanel.Controls.Clear();
        foreach (var m in snap.Weekly.Models.Take(8))
        {
            _modelsPanel.Controls.Add(new Label
            {
                AutoSize  = false,
                Width     = InnerWidth,
                Height    = 14,
                Font      = new Font("Segoe UI", 7.5f),
                ForeColor = TextPrimary,
                Text      = $"{Trunc(m.Name, 20),-20}  {m.RequestCount:N0} req",
            });
        }

        bool hasModels = snap.Weekly.Models.Count > 0;
        _modelsPanel.Visible  = hasModels;
        _modelsHeader.Visible = hasModels;

        ArrangeControls();

        int lastBottom = _panel.Controls
            .OfType<Control>()
            .Where(c => c.Visible)
            .Select(c => c.Bottom)
            .DefaultIfEmpty(MinHeight)
            .Max();
        this.Height = Math.Max(lastBottom + Pad * 2, MinHeight);
    }

    private void PositionNear(Point cursor)
    {
        Screen screen = Screen.FromPoint(cursor);
        Rectangle area = screen.WorkingArea;

        int x = cursor.X - this.Width / 2;
        int y = cursor.Y - this.Height - 12; // above cursor

        x = Math.Clamp(x, area.Left, area.Right  - this.Width);
        y = Math.Clamp(y, area.Top,  area.Bottom - this.Height);

        Location = new Point(x, y);
    }

    private void OnPaint(object? sender, PaintEventArgs e)
    {
        using var pen = new Pen(BorderColor, 1);
        e.Graphics.DrawRectangle(pen, 0, 0, this.Width - 1, this.Height - 1);
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Panel MakeSpacer(int h) =>
        new() { Height = h, Width = InnerWidth, BackColor = BgColor };

    private static string Trunc(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _hideTimer.Dispose();
            _panel.Dispose();
        }
        base.Dispose(disposing);
    }
}
