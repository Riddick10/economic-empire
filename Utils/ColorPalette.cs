using Raylib_cs;

namespace GrandStrategyGame.UI;

/// <summary>
/// Zentrale Farbpalette fuer die UI
/// </summary>
public static class ColorPalette
{
    // === UI-HINTERGRUND ===
    public static readonly Color Background = new(30, 30, 40, 255);
    public static readonly Color BackgroundDark = new(20, 20, 30, 255);
    public static readonly Color BackgroundLight = new(40, 40, 60, 255);
    public static readonly Color Panel = new(45, 45, 60, 255);
    public static readonly Color PanelLight = new(65, 65, 85, 255);
    public static readonly Color PanelDark = new(35, 35, 50, 255);

    // === AKZENTE ===
    public static readonly Color Accent = new(100, 150, 255, 255);
    public static readonly Color AccentLight = new(130, 175, 255, 255);
    public static readonly Color AccentDark = new(70, 120, 220, 255);

    // === TEXT ===
    public static readonly Color TextWhite = new(240, 240, 240, 255);
    public static readonly Color TextGray = new(150, 150, 160, 255);
    public static readonly Color TextDark = new(100, 100, 110, 255);

    // === STATUS ===
    public static readonly Color Green = new(100, 200, 100, 255);
    public static readonly Color GreenDark = new(30, 80, 30, 255);
    public static readonly Color Red = new(200, 100, 100, 255);
    public static readonly Color RedDark = new(80, 30, 30, 255);
    public static readonly Color Yellow = new(200, 200, 100, 255);
    public static readonly Color YellowDark = new(80, 80, 30, 255);
    public static readonly Color Orange = new(255, 165, 0, 255);

    // === BUTTON HOVER ===
    public static readonly Color ButtonHover = new(60, 60, 80, 255);
    public static readonly Color ButtonNormal = new(40, 40, 60, 255);

    /// <summary>
    /// Gibt eine transparente Panel-Farbe zurueck
    /// </summary>
    public static Color PanelTransparent(byte alpha = 230)
        => new(Panel.R, Panel.G, Panel.B, alpha);

}
