using System.Drawing.Imaging;
using System.Drawing.Text;

namespace TokenPeek;

/// <summary>
/// Renders a small icon for a <see cref="NotifyIcon"/> showing a used-% number.
///
/// Normal-state colours are caller-supplied (from <see cref="AppConfig"/>).
/// Warning thresholds override the background (orange ≥ 80% used, red ≥ 90% used)
/// but never override the text colour — the text the user chose stays readable.
/// </summary>
public static class TrayIconRenderer
{
    private const int Size = 32;

    // Warning/critical thresholds are on the USED side.
    private const int WarnUsedThreshold = 80;
    private const int CritUsedThreshold = 90;

    private static readonly Color WarnColor = Color.FromArgb(0xE6, 0x8A, 0x00); // orange
    private static readonly Color CritColor = Color.FromArgb(0xCC, 0x22, 0x22); // red

    /// <summary>
    /// Creates an <see cref="Icon"/> from a used fraction and caller-supplied colours.
    /// Caller is responsible for disposing the returned icon.
    /// </summary>
    /// <param name="usedFraction">0–1 fraction <em>used</em>.</param>
    /// <param name="bgColor">Normal-state background (overridden by warning/critical thresholds).</param>
    /// <param name="textColor">Text colour (never overridden by thresholds).</param>
    public static Icon Create(double usedFraction, Color bgColor, Color textColor)
    {
        int pct   = (int)Math.Round(Math.Clamp(usedFraction, 0.0, 1.0) * 100);
        Color bg  = PickBackground(pct, bgColor);
        string label = pct.ToString();

        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
            g.Clear(bg);

            using var font  = FitFont(g, label, Size);
            using var brush = new SolidBrush(textColor);
            using var fmt   = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(label, font, brush, new RectangleF(0, 0, Size, Size), fmt);
        }

        IntPtr hIcon = bmp.GetHicon();
        Icon icon  = Icon.FromHandle(hIcon);
        Icon clone = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        icon.Dispose();
        return clone;
    }

    /// <summary>Convenience overload that reads colours from an <see cref="IconColorEntry"/>.</summary>
    public static Icon Create(double usedFraction, IconColorEntry colors) =>
        Create(usedFraction, colors.BgColor, colors.TextColor);

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static Color PickBackground(int usedPct, Color normalBg)
    {
        if (usedPct >= CritUsedThreshold) return CritColor;
        if (usedPct >= WarnUsedThreshold) return WarnColor;
        return normalBg;
    }

    /// <summary>
    /// Returns the largest font (Arial Narrow, regular) where <paramref name="text"/>
    /// fits inside a <paramref name="cellSize"/>-pixel square, with a 1 px margin.
    /// Steps down from 30 px.
    /// </summary>
    private static Font FitFont(Graphics g, string text, int cellSize)
    {
        float maxDim = cellSize - 2f;
        for (float size = 30f; size >= 6f; size -= 1f)
        {
            var font = new Font("Arial Narrow", size, FontStyle.Regular, GraphicsUnit.Pixel);
            SizeF measured = g.MeasureString(text, font);
            if (measured.Width <= maxDim && measured.Height <= maxDim)
                return font;
            font.Dispose();
        }
        return new Font("Arial Narrow", 6f, FontStyle.Regular, GraphicsUnit.Pixel);
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
