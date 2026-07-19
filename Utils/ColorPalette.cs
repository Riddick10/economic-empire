using Raylib_cs;

namespace GrandStrategyGame.UI;

/// <summary>
/// Zentrale Farbpalette fuer die UI
/// </summary>
public static class ColorPalette
{
    // === UI-HINTERGRUND (Living-World-Stil: dunkles Glas) ===
    public static readonly Color Background = new(12, 16, 27, 255);
    public static readonly Color BackgroundDark = new(8, 10, 18, 255);
    public static readonly Color BackgroundLight = new(30, 38, 58, 255);
    public static readonly Color Panel = new(16, 20, 32, 255);
    public static readonly Color PanelLight = new(46, 56, 82, 255);
    public static readonly Color PanelDark = new(10, 13, 22, 255);

    // === AKZENTE (Gold wie im Hauptmenue) ===
    public static readonly Color Accent = new(230, 185, 80, 255);
    public static readonly Color AccentLight = new(255, 215, 130, 255);
    public static readonly Color AccentDark = new(176, 136, 52, 255);

    // === TEXT ===
    public static readonly Color TextWhite = new(240, 243, 250, 255);
    public static readonly Color TextGray = new(150, 156, 172, 255);
    public static readonly Color TextDark = new(105, 110, 125, 255);

    // === STATUS ===
    public static readonly Color Green = new(100, 200, 100, 255);
    public static readonly Color GreenDark = new(30, 80, 30, 255);
    public static readonly Color Red = new(200, 100, 100, 255);
    public static readonly Color RedDark = new(80, 30, 30, 255);
    public static readonly Color Yellow = new(200, 200, 100, 255);
    public static readonly Color YellowDark = new(80, 80, 30, 255);
    public static readonly Color Orange = new(255, 165, 0, 255);

    // === BUTTON HOVER (dunkles Glas -> heller/blauer beim Hover) ===
    public static readonly Color ButtonHover = new(36, 46, 72, 255);
    public static readonly Color ButtonNormal = new(20, 26, 40, 255);

    /// <summary>
    /// Gibt eine transparente Panel-Farbe zurueck
    /// </summary>
    public static Color PanelTransparent(byte alpha = 230)
        => new(Panel.R, Panel.G, Panel.B, alpha);

}
