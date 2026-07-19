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

        const string label = "ZURÜCK";
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
    /// Rechteck in beide Richtungen vergroessern (fuer grosszuegige Klick-Flaechen)
    /// </summary>
    public static Rectangle Inflate(Rectangle r, float dx, float dy) =>
        new(r.X - dx, r.Y - dy, r.Width + dx * 2, r.Height + dy * 2);

    /// <summary>
    /// Dunkler Glas-Panel-Rahmen: Schatten, Glas, Lichtkante, Rand, Goldkante oben.
    /// Der gemeinsame Fenster-Look aller Menues und In-Game-Panels.
    /// </summary>
    public static void DrawGlassPanel(Rectangle panel, float a = 1f)
    {
        byte A(float v) => (byte)Math.Clamp(v * a, 0, 255);
        int px = (int)panel.X, py = (int)panel.Y, pw = (int)panel.Width, ph = (int)panel.Height;

        Raylib.DrawRectangle(px + 4, py + 6, pw, ph, new Color((byte)0, (byte)0, (byte)0, A(90)));
        Raylib.DrawRectangleRec(panel, new Color((byte)12, (byte)16, (byte)27, A(244)));
        Raylib.DrawRectangle(px, py + 3, pw, 1, new Color((byte)255, (byte)255, (byte)255, A(22)));
        Raylib.DrawRectangleLinesEx(panel, 1, new Color((byte)80, (byte)88, (byte)110, A(170)));
        Raylib.DrawRectangle(px, py, pw, 3, Gold(A(210)));
    }

    /// <summary>
    /// Panel-Titel: Goldmarke + Titel in warmem Weiss. Gibt Y unter dem Titel zurueck.
    /// </summary>
    public static int DrawPanelTitle(string title, int x, int y, int fontSize = 26)
    {
        Raylib.DrawRectangle(x, y + 4, 5, fontSize - 6, Gold(210));
        Program.DrawGameText(title, x + 13, y, fontSize, new Color((byte)245, (byte)240, (byte)228, (byte)255));
        return y + fontSize + 10;
    }

    /// <summary>
    /// Kleiner Glas-Tooltip mit Goldmarke links (fuer Karten-Hover u.ae.)
    /// </summary>
    public static void DrawTooltip(Rectangle r)
    {
        Raylib.DrawRectangleRounded(new Rectangle(r.X + 3, r.Y + 4, r.Width, r.Height), 0.12f, 6,
            new Color((byte)0, (byte)0, (byte)0, (byte)90));
        Raylib.DrawRectangleRounded(r, 0.12f, 6, new Color((byte)12, (byte)16, (byte)27, (byte)242));
        Raylib.DrawRectangleRoundedLinesEx(r, 0.12f, 6, 1, new Color((byte)80, (byte)88, (byte)110, (byte)180));
        Raylib.DrawRectangleRounded(new Rectangle(r.X, r.Y + 4, 4, r.Height - 8), 0.5f, 4, Gold(220));
    }

    /// <summary>
    /// Abschnitts-Ueberschrift: goldene Marke + Titel + feine Linie
    /// </summary>
    public static void DrawOptionsSection(string title, int x, int y, int width, float a)
    {
        byte A(float v) => (byte)Math.Clamp(v * a, 0, 255);
        Raylib.DrawRectangle(x, y + 2, 4, 13, Gold(A(220)));
        Program.DrawGameText(title, x + 12, y, 15, Gold(A(235)));
        int tw = Program.MeasureTextCached(title, 15);
        Raylib.DrawRectangle(x + 12 + tw + 12, y + 8, Math.Max(0, width - tw - 24), 1,
            new Color((byte)255, (byte)255, (byte)255, A(22)));
    }

    /// <summary>
    /// Slider mit goldener Fuellung und rundem Knauf (Glow bei Hover/Drag)
    /// </summary>
    public static void DrawOptionSlider(string label, Rectangle track, float value, bool active, float a)
    {
        byte A(float v) => (byte)Math.Clamp(v * a, 0, 255);
        Color gold = Gold(A(255));

        int labelY = (int)track.Y - 30;
        Program.DrawGameText(label, (int)track.X, labelY, 17,
            new Color((byte)210, (byte)215, (byte)228, A(235)));

        string pct = $"{(int)(value * 100)}%";
        int pctW = Program.MeasureTextCached(pct, 17);
        Program.DrawGameText(pct, (int)(track.X + track.Width) - pctW, labelY, 17, gold);

        Raylib.DrawRectangleRounded(track, 0.5f, 6, new Color((byte)5, (byte)7, (byte)13, A(235)));
        Raylib.DrawRectangleRoundedLinesEx(track, 0.5f, 6, 1, new Color((byte)70, (byte)78, (byte)100, A(160)));

        int fillW = (int)(track.Width * value);
        if (fillW > 3)
        {
            Raylib.DrawRectangleRounded(new Rectangle(track.X, track.Y, fillW, track.Height), 0.5f, 6,
                new Color((byte)230, (byte)185, (byte)80, A(235)));
        }

        float kx = track.X + fillW;
        float ky = track.Y + track.Height / 2f;
        if (active)
            Raylib.DrawCircleV(new Vector2(kx, ky), 14, new Color((byte)230, (byte)185, (byte)80, A(55)));
        Raylib.DrawCircleV(new Vector2(kx, ky), 8, new Color((byte)244, (byte)246, (byte)252, A(255)));
        Raylib.DrawCircleLines((int)kx, (int)ky, 8, gold);
    }

    /// <summary>
    /// Toggle als Pille mit gleitendem Knopf; t = Animationswert 0..1 (aus/an)
    /// </summary>
    public static void DrawTogglePill(Rectangle toggle, float t, float a = 1f)
    {
        byte A(float v) => (byte)Math.Clamp(v * a, 0, 255);
        Raylib.DrawRectangleRounded(toggle, 1f, 10,
            Lerp(new Color((byte)10, (byte)13, (byte)22, A(230)),
                 new Color((byte)176, (byte)136, (byte)52, A(240)), t));
        Raylib.DrawRectangleRoundedLinesEx(toggle, 1f, 10, 1,
            Lerp(new Color((byte)70, (byte)78, (byte)100, A(180)), Gold(A(255)), t));
        float knobX = toggle.X + 14 + (toggle.Width - 28) * t;
        Raylib.DrawCircleV(new Vector2(knobX, toggle.Y + toggle.Height / 2f), 10,
            new Color((byte)240, (byte)243, (byte)250, A(255)));
    }

    /// <summary>
    /// Rundes Schliessen-Kreuz (rot beim Hover)
    /// </summary>
    public static void DrawCloseButton(Rectangle r, float hover, float a = 1f)
    {
        byte A(float v) => (byte)Math.Clamp(v * a, 0, 255);
        var center = new Vector2(r.X + r.Width / 2f, r.Y + r.Height / 2f);

        if (hover > 0.02f)
            Raylib.DrawCircleV(center, r.Width / 2f, new Color((byte)205, (byte)80, (byte)70, A(60 + hover * 120)));
        Raylib.DrawRing(center, r.Width / 2f - 1, r.Width / 2f, 0, 360, 28,
            Lerp(new Color((byte)110, (byte)118, (byte)140, A(170)),
                 new Color((byte)235, (byte)110, (byte)100, A(255)), hover));

        Color x = Lerp(new Color((byte)190, (byte)196, (byte)210, A(220)),
                       new Color((byte)255, (byte)255, (byte)255, A(255)), hover);
        const float s = 5.5f;
        Raylib.DrawLineEx(new Vector2(center.X - s, center.Y - s), new Vector2(center.X + s, center.Y + s), 2.2f, x);
        Raylib.DrawLineEx(new Vector2(center.X - s, center.Y + s), new Vector2(center.X + s, center.Y - s), 2.2f, x);
    }

    /// <summary>
    /// Zahnrad-Icon aus Primitiven (fuer Options-Titel)
    /// </summary>
    public static void DrawGearIcon(float cx, float cy, Color c)
    {
        var center = new Vector2(cx, cy);
        Raylib.DrawRing(center, 4.5f, 7.5f, 0, 360, 24, c);
        for (int t = 0; t < 4; t++)
        {
            Raylib.DrawRectanglePro(new Rectangle(cx, cy, 4, 21),
                new Vector2(2, 10.5f), t * 45f, c);
        }
        Raylib.DrawRing(center, 4.5f, 7.5f, 0, 360, 24, c);
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
