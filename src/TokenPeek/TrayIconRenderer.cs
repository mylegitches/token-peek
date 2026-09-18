using System.Drawing.Imaging;
using System.Drawing.Text;

namespace TokenPeek;

/// <summary>Renders a small icon for a <see cref="NotifyIcon"/> showing a remaining-% number.</summary>
public static class TrayIconRenderer
{
    // Icon pixel size (Windows will scale as needed).
    private const int Size = 32;

    // Remaining-% colour thresholds.
    private const int WarnThreshold = 20;
    private const int CritThreshold = 10;

    // Session = blue family, Weekly = green family (base colours, may be overridden by warnings).
    private static readonly Color SessionBaseColor = Color.FromArgb(0x1E, 0x6F, 0xC8); // blue
    private static readonly Color WeeklyBaseColor  = Color.FromArgb(0x1A, 0x8A, 0x42); // green
    private static readonly Color WarnColor        = Color.FromArgb(0xE6, 0x8A, 0x00); // orange
    private static readonly Color CritColor        = Color.FromArgb(0xCC, 0x22, 0x22); // red

    /// <summary>
    /// Creates an <see cref="Icon"/> from a remaining percentage and window type.
    /// Caller is responsible for disposing the returned icon.
    /// </summary>
    /// <param name="usedFraction">Value in [0, 1] — fraction <em>used</em>.</param>
    /// <param name="isSession"><c>true</c> for session (blue), <c>false</c> for weekly (green).</param>
    public static Icon Create(double usedFraction, bool isSession)
    {
        int pct = (int)Math.Round(Math.Clamp(usedFraction, 0.0, 1.0) * 100);

        Color bg    = PickBackground(pct, isSession);
        string label = pct.ToString();

        using var bmp = new Bitmap(Size, Size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            // Pixel-grid hint keeps digits crisp when Windows scales 32→16.
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;

            // Solid background.
            g.Clear(bg);

            using var brush = new SolidBrush(Color.White);
            using var fmt   = new StringFormat(StringFormatFlags.NoWrap)
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };

            // Auto-fit: find the largest font where the label stays inside the bitmap.
            using var font = FitFont(g, label, Size);
            g.DrawString(label, font, brush, new RectangleF(0, 0, Size, Size), fmt);
        }

        IntPtr hIcon = bmp.GetHicon();
        Icon icon  = Icon.FromHandle(hIcon);
        Icon clone = (Icon)icon.Clone();
        DestroyIcon(hIcon);
        icon.Dispose();
        return clone;
    }

    /// <summary>
    /// Returns the largest font (not bold, Arial Narrow) where <paramref name="text"/>
    /// fits within <paramref name="cellSize"/> pixels on each axis.
    /// Starts at 30 px and steps down by 1 until it fits.
    /// </summary>
    private static Font FitFont(Graphics g, string text, int cellSize)
    {
        // Leave a 1-pixel margin on each side so glyphs don't touch the edge.
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

    // Thresholds are now on the USED side: warn when used is high.
    private static Color PickBackground(int usedPct, bool isSession)
    {
        if (usedPct >= (100 - CritThreshold))  return CritColor;   // ≥ 90% used
        if (usedPct >= (100 - WarnThreshold))  return WarnColor;   // ≥ 80% used
        return isSession ? SessionBaseColor : WeeklyBaseColor;
    }

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr hIcon);
}
