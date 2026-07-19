using Raylib_cs;
using System.Numerics;

namespace GrandStrategyGame.UI;

/// <summary>
/// Gemeinsame Zeichen-Helfer fuer den "Living World"-Menuestil:
/// dunkles Glas, Goldakzente, Chevrons.
/// Verwendet von Hauptmenue, Options-Overlay und Untermenue-Screens.
/// </summary>
internal static class MenuStyle
{
    public static Color Gold(byte a = 255) => new((byte)230, (byte)185, (byte)80, a);
    public static Color GoldBright(byte a = 255) => new((byte)255, (byte)215, (byte)130, a);

    public static Color Lerp(Color a, Color b, float t)
    {
        t = Math.Clamp(t, 0f, 1f);
        return new Color(
            (byte)(a.R + (b.R - a.R) * t),
            (byte)(a.G + (b.G - a.G) * t),
            (byte)(a.B + (b.B - a.B) * t),
            (byte)(a.A + (b.A - a.A) * t));
    }

    /// <summary>
    /// Dunkle Glas-Karte mit Schatten, Lichtkante, Rand und goldenem
    /// Akzentbalken links, der beim Hover waechst und glueht
    /// </summary>
    public static void DrawGlassCard(Rectangle r, float hover, float alpha = 1f)
    {
        byte A(float v) => (byte)Math.Clamp(v * alpha, 0, 255);

        // Schatten
        Raylib.DrawRectangle((int)r.X + 3, (int)r.Y + 4, (int)r.Width, (int)r.Height,
            new Color((byte)0, (byte)0, (byte)0, A(70)));

        // Dunkles Glas -> beim Hover heller und blauer
        Color bg = Lerp(new Color((byte)14, (byte)18, (byte)30, A(205)),
                        new Color((byte)36, (byte)46, (byte)72, A(240)), hover);
        Raylib.DrawRectangleRec(r, bg);

        // Feiner heller Streifen oben (Glas-Effekt)
        Raylib.DrawRectangle((int)r.X, (int)r.Y, (int)r.Width, 1,
            new Color((byte)255, (byte)255, (byte)255, A(18 + hover * 25)));

        // Rand
        Color border = Lerp(new Color((byte)80, (byte)88, (byte)110, (byte)255), ColorPalette.Accent, hover);
        border.A = A(150 + hover * 105);
        Raylib.DrawRectangleLinesEx(r, 1, border);

        // Goldener Akzentbalken links
        int barW = (int)(4 + hover * 5);
        Raylib.DrawRectangle((int)r.X, (int)r.Y, barW, (int)r.Height, Gold(A(190 + hover * 65)));
        if (hover > 0.05f)
        {
            Raylib.DrawRectangleGradientH((int)r.X + barW, (int)r.Y, (int)(34 * hover), (int)r.Height,
                Gold(A(45 * hover)), Gold(0));
        }
    }

    /// <summary>
    /// Chevron (Winkel) - direction: +1 = zeigt nach rechts, -1 = nach links
    /// </summary>
    public static void DrawChevron(float cx, float cy, int direction, Color c)
    {
        float d = direction >= 0 ? 1f : -1f;
        Raylib.DrawLineEx(new Vector2(cx - 5 * d, cy - 7), new Vector2(cx + 2 * d, cy), 2.5f, c);
        Raylib.DrawLineEx(new Vector2(cx + 2 * d, cy), new Vector2(cx - 5 * d, cy + 7), 2.5f, c);
    }

    /// <summary>
    /// Zurueck-Button im Stil der Hauptmenue-Buttons (Chevron zeigt nach links)
    /// </summary>
    public static void DrawBackButton(Rectangle r, float hover, float alpha = 1f)
    {
        byte A(float v) => (byte)Math.Clamp(v * alpha, 0, 255);
        DrawGlassCard(r, hover, alpha);

        const string label = "ZURUECK";
        int textW = Program.MeasureTextCached(label, GameConfig.FONT_SIZE_LARGE);
        int textX = (int)(r.X + (r.Width - textW) / 2f + 8);
        int textY = (int)(r.Y + (r.Height - GameConfig.FONT_SIZE_LARGE) / 2f);

        DrawChevron(textX - 18 - hover * 4, r.Y + r.Height / 2f, -1,
            new Color((byte)255, (byte)215, (byte)130, A(160 + hover * 95)));

        Color textColor = Lerp(new Color((byte)205, (byte)210, (byte)222, A(235)),
                               new Color((byte)255, (byte)255, (byte)255, A(255)), hover);
        Program.DrawGameText(label, textX, textY, GameConfig.FONT_SIZE_LARGE, textColor);
    }

    /// <summary>
    /// Bildschirm-Titel in warmem Weiss mit goldener Gradient-Unterstreichung
    /// </summary>
    public static void DrawScreenTitle(string title, int centerX, int y, int fontSize = 34)
    {
        int titleW = Program.MeasureTextCached(title, fontSize);
        int titleX = centerX - titleW / 2;

        Program.DrawGameText(title, titleX + 2, y + 2, fontSize, new Color((byte)0, (byte)0, (byte)0, (byte)140));
        Program.DrawGameText(title, titleX, y, fontSize, new Color((byte)245, (byte)240, (byte)228, (byte)255));

        // Gradient-Linie (transparent -> Gold -> transparent)
        int lineY = y + fontSize + 10;
        int lineW = titleW + 60;
        int lineX = centerX - lineW / 2;
        for (int i = 0; i <= lineW; i++)
        {
            float t = (float)i / lineW;
            float a = 1.0f - 2.0f * Math.Abs(t - 0.5f);
            Raylib.DrawLine(lineX + i, lineY, lineX + i, lineY + 1, Gold((byte)(a * 200)));
        }
    }
}
